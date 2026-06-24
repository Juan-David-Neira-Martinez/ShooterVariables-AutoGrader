# Shooter de Variables con Auto-Grader

Proyecto Unity desarrollado como sistema gamificado para el aprendizaje de variables en programación, incorporando un módulo de evaluación automática basado en IA para clasificar respuestas de estudiantes y generar retroalimentación.

## Objetivo del proyecto

El objetivo es integrar un módulo Auto-Grader al videojuego educativo para registrar respuestas de los estudiantes, construir datasets reales y sintéticos, entrenar modelos base de clasificación y entregar retroalimentación automática dentro del juego.

## Componentes principales

* **Unity Game:** videojuego educativo tipo shooter con preguntas sobre variables en programación.
* **Auto-Grader API:** servicio local en Python/FastAPI que evalúa respuestas.
* **Dataset real:** logs generados automáticamente por Unity durante la interacción del jugador.
* **Dataset sintético:** conjunto de ejemplos generados a partir del banco de preguntas y la taxonomía de errores.
* **Modelo baseline:** clasificación con TF-IDF + Logistic Regression.
* **Retroalimentación:** mensajes asociados a etiquetas `OK`, `E1`, `E2`, `E3`, `E4`, `E5` y `E6`.

## Estructura del proyecto

```txt
Assets/
Packages/
ProjectSettings/
PythonAutoGrader/
data/
docs/
```

## Dataset real

El dataset real se genera automáticamente desde Unity en cada intento-respuesta del jugador. Cada fila contiene:

```txt
user_id_anon
session_id
timestamp
item_id
prompt
answer_raw
answer_type
difficulty
attempt_n
time_sec
hint_used
y_label
model_label
confidence
feedback_text
```

## Ejecución del Auto-Grader

Entrar a la carpeta:

```bash
cd PythonAutoGrader
```

Instalar dependencias:

```bash
python -m pip install -r requirements.txt
```

Generar dataset sintético:

```bash
python generate_synthetic_dataset.py
```

Entrenar modelo baseline:

```bash
python train_baseline.py
```

Ejecutar API:

```bash
python -m uvicorn app:app --reload --host 127.0.0.1 --port 8000
```

## Integración con Unity

En Unity, el script principal de integración es:

```txt
Assets/juego/Scripts/UI/interactuar.cs
```

Para usar la API, cada objeto de pregunta debe tener:

```txt
useApiEvaluation = true
evaluateUrl = http://127.0.0.1:8000/evaluate
apiOkMinConfidence = 0.60
```

## Evidencias

Las evidencias del proyecto se encuentran en:

```txt
data/real_raw/
data/real_labeled/
data/synthetic/
docs/evidencias/
```

## Estado actual

* Logging real desde Unity implementado.
* Dataset real crudo generado.
* Dataset real etiquetado manualmente.
* Dataset sintético generado.
* Modelo baseline entrenado.
* API local conectada con Unity.
* Retroalimentación automática integrada.
* Mejoras de accesibilidad en el campo de respuesta.
