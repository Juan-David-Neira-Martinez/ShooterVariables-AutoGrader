# Informe de integración Unity–Auto-Grader

## Resumen de la prueba

- Registros totales: 21
- Usuarios anónimos: 1
- Sesiones: 1
- Ítems diferentes: 15
- Tiempo promedio de respuesta: 4.46 segundos
- Tiempo mediano de respuesta: 4.18 segundos

## Distribución de predicciones del modelo (`model_label`)

- E1: 6
- OK: 15

## Distribución de etiquetas revisadas (`y_label`)

- E1: 1
- E2: 2
- E3: 1
- E4: 1
- OK: 16

## Diagnóstico API vs. revisión manual

- Exactitud multiclase directa (`model_label` vs `y_label`): 0.7619
- Exactitud binaria OK/No-OK: 0.9524
- Precisión OK: 1.0000
- Recall OK: 0.9375
- Verdaderos positivos OK: 15
- Verdaderos negativos No-OK: 5
- Falsos positivos OK: 0
- Falsos negativos OK: 1

## Hallazgos

1. La integración Unity–API funciona correctamente: Unity envía las respuestas, recibe `model_label`, `confidence` y `feedback_text`, y guarda todo en `logs_autograder.csv`.
2. Ya no aparece el problema de aceptar respuestas aleatorias como `asd`, porque la API y Unity aplican control por confianza y reglas para respuestas no informativas.
3. El dataset ya no presenta `ITEM_001` ni `Pregunta sin definir`; los ítems aparecen identificados como `VARS_001...VARS_015`.
4. Se detectó un caso de respuesta semánticamente correcta rechazada por no estar incluida como alternativa exacta: `32` para `VARS_012`. Se recomienda agregar `32 bits|32`.
5. Las mejoras de UI e interacción quedaron validadas funcionalmente: el log no muestra envíos vacíos accidentales ni repeticiones por puerta ya abierta.

## Archivos generados

- `dataset_real_labeled_integracion_api.csv`
- `dataset_real_labeled_integracion_api_revision.csv`
- `respuestas_correctas_recomendadas_unity_v2.csv`
