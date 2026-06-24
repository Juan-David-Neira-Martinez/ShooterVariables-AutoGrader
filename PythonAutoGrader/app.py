from fastapi import FastAPI
from pydantic import BaseModel
import joblib
import numpy as np
import os
import re
import unicodedata

MODEL_PATH = "autograder_baseline.joblib"
MIN_OK_CONFIDENCE = 0.60

FEEDBACK = {
    "OK": "Respuesta correcta. Puedes avanzar.",
    "E1": "La respuesta no permite evidenciar comprensión suficiente. Revisa el concepto y vuelve a intentarlo.",
    "E2": "Revisa la diferencia entre declarar, inicializar y asignar una variable.",
    "E3": "Revisa el tipo de dato esperado, su valor por defecto o su rango válido.",
    "E4": "Revisa la escritura de palabras clave, identificadores y reglas de nombrado.",
    "E5": "Revisa paso a paso la operación o expresión sobre la variable.",
    "E6": "Revisa el ámbito de la variable: local, de instancia, de clase o global según el contexto.",
    "LOW_CONFIDENCE": "No hay suficiente confianza para aceptar la respuesta. Revisa el enunciado e inténtalo de nuevo.",
    "API_ERROR": "No se pudo evaluar la respuesta.",
}

class EvalRequest(BaseModel):
    item_id: str
    prompt: str
    answer_raw: str
    answer_type: str
    difficulty: str
    correct_answer: str = ""

app = FastAPI(title="AutoGrader API")

model = None

def normalize(text: str) -> str:
    if text is None:
        return ""
    text = text.strip().lower()
    text = unicodedata.normalize("NFD", text)
    text = "".join(c for c in text if unicodedata.category(c) != "Mn")
    text = text.replace("’", "'").replace("`", "'")
    text = re.sub(r"\s+", " ", text)
    return text.strip()

def exact_match(answer: str, correct_answer: str) -> bool:
    answer_n = normalize(answer)
    accepted = [normalize(x) for x in (correct_answer or "").split("|") if normalize(x)]
    return bool(answer_n and answer_n in accepted)

def is_non_informative(answer: str) -> bool:
    a = normalize(answer)
    if not a:
        return True

    non_info = {
        "no", "no se", "nose", "no sé", "n/a", "na", "ninguna",
        "asd", "ads", "sad", "sdsd", "ad", "as", "ddddddddddd", "d",
        "x", "?", "??", "error", "no entiendo"
    }

    if a in non_info:
        return True

    # Rechaza cadenas muy cortas solo de letras, salvo que luego hagan exact_match.
    if re.fullmatch(r"[a-z]{1,3}", a):
        return True

    # Rechaza repeticiones del mismo carácter.
    if len(set(a)) <= 2 and len(a) >= 4:
        return True

    return False

@app.on_event("startup")
def load_model():
    global model
    if os.path.exists(MODEL_PATH):
        model = joblib.load(MODEL_PATH)

@app.post("/evaluate")
def evaluate(req: EvalRequest):
    answer = req.answer_raw or ""

    # 1. Regla determinista: si coincide con correctAnswer o alguna alternativa, aceptar.
    if exact_match(answer, req.correct_answer):
        return {
            "label": "OK",
            "confidence": 1.0,
            "feedback_id": "OK",
            "feedback_text": FEEDBACK["OK"],
        }

    # 2. Regla determinista: respuestas vacías/no informativas no se aceptan.
    if is_non_informative(answer):
        return {
            "label": "E1",
            "confidence": 1.0,
            "feedback_id": "E1",
            "feedback_text": FEEDBACK["E1"],
        }

    # 3. Modelo baseline.
    if model is None:
        return {
            "label": "API_ERROR",
            "confidence": 0.0,
            "feedback_id": "API_ERROR",
            "feedback_text": FEEDBACK["API_ERROR"],
        }

    text = req.prompt + " [SEP] " + answer
    label = model.predict([text])[0]

    confidence = 0.0
    try:
        proba = model.predict_proba([text])[0]
        confidence = float(np.max(proba))
    except Exception:
        confidence = 0.0

    # 4. Seguridad: nunca aceptar OK con confianza baja.
    if label == "OK" and confidence < MIN_OK_CONFIDENCE:
        return {
            "label": "E1",
            "confidence": confidence,
            "feedback_id": "LOW_CONFIDENCE",
            "feedback_text": FEEDBACK["LOW_CONFIDENCE"],
        }

    return {
        "label": label,
        "confidence": confidence,
        "feedback_id": label,
        "feedback_text": FEEDBACK.get(label, "Revisa tu respuesta y vuelve a intentarlo."),
    }