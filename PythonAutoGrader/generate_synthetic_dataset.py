import csv
import random

LABELS = ["OK", "E1", "E2", "E3", "E4", "E5", "E6"]

TEMPLATES = {
    "OK": [
        "{correct_answer}",
        "La respuesta es {correct_answer}",
        "Es {correct_answer}",
    ],
    "E1": [
        "Una variable es una función.",
        "Una variable es un método.",
        "Una variable es una instrucción que ejecuta código.",
        "no se",
        "no sé",
        "asd",
        "ad",
        "sdsd",
        "error",
        "no entiendo",
    ],
    "E2": [
        "Declarar e inicializar son exactamente lo mismo.",
        "Asignar es solo escribir el nombre de la variable.",
        "Inicializar no necesita valor.",
    ],
    "E3": [
        "El tipo de dato no importa.",
        "Todo valor debe ser string.",
        "Siempre debe ser entero.",
    ],
    "E4": [
        "Una variable puede empezar por número.",
        "Puede llamarse con símbolos raros.",
        "El nombre no necesita tener significado.",
    ],
    "E5": [
        "El resultado es 3.",
        "La variable no cambia después de la operación.",
        "El resultado se mantiene igual que al inicio.",
    ],
    "E6": [
        "Todas las variables son globales.",
        "No existe diferencia entre local y global.",
        "Una variable local siempre modifica la global.",
    ],
}

def read_items(path):
    with open(path, newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))

def main():
    items = read_items("sample_items.csv")
    rows = []

    for item in items:
        for label in LABELS:
            for _ in range(5):
                answer = random.choice(TEMPLATES[label]).format(**item)
                rows.append({
                    "user_id_anon": "synthetic",
                    "session_id": "synthetic",
                    "timestamp": "",
                    "item_id": item["item_id"],
                    "prompt": item["prompt"],
                    "answer_raw": answer,
                    "answer_type": item["answer_type"],
                    "difficulty": item["difficulty"],
                    "attempt_n": random.randint(1, 3),
                    "time_sec": round(random.uniform(5, 60), 2),
                    "hint_used": random.randint(0, 1),
                    "y_label": label,
                })

    random.shuffle(rows)

    with open("dataset_sintetico.csv", "w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)

    print(f"Dataset sintético creado: {len(rows)} filas")

if __name__ == "__main__":
    main()