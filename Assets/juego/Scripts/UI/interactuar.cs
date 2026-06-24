using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;

public class Interatuar : MonoBehaviour
{
    [Header("UI actual")]
    public TMP_InputField inputField;
    public TMP_Text wallText;
    public string correctAnswer;

    [Header("Puerta")]
    public Transform door;                 // Arrastra aquí el objeto de la puerta
    public float doorOpenHeight = 3f;      // Distancia a levantar la puerta
    public float doorOpenSpeed = 2f;       // Velocidad de apertura
    public MonoBehaviour player;

    [Header("Auto-Grader / Dataset")]
    public string itemId = "ITEM_001";
    [TextArea(2, 5)] public string prompt = "Pregunta sin definir";
    public string answerType = "short_text_1sent"; // short_text_1sent / mcq_single
    public string difficulty = "easy";             // easy / medium / hard
    public bool hintUsed = false;

    [Header("API local opcional")]
    public bool useApiEvaluation = false;
    public string evaluateUrl = "http://127.0.0.1:8000/evaluate";
    [Range(0f, 1f)] public float apiOkMinConfidence = 0.60f;

    [Header("Control de interacción")]
    public bool blockInteractionAfterCorrectAnswer = true;
    public bool disableColliderAfterCorrectAnswer = false;
    public bool ignoreEmptyAnswers = true;

    [Header("Accesibilidad del campo de respuesta")]
    public bool applyInputAccessibilitySettings = true;
    public Vector2 inputFieldSize = new Vector2(760f, 95f);
    [Range(12f, 72f)] public float inputTextFontSize = 34f;
    [Range(12f, 72f)] public float placeholderFontSize = 28f;
    public string placeholderText = "Escribe tu respuesta y presiona Enter";
    public bool singleLineInput = true;

    private bool isDoorOpen = false;
    private bool questionCompleted = false;
    private bool isInputOpen = false;
    private bool isEvaluating = false;

    private Vector3 doorClosedPosition; // Posición inicial de la puerta
    private Collider interactionCollider;

    private string userIdAnon;
    private string sessionId;
    private int attemptNumber = 0;
    private float questionStartTime = 0f;

    void Start()
    {
        if (door != null)
            doorClosedPosition = door.position;

        interactionCollider = GetComponent<Collider>();

        userIdAnon = PlayerPrefs.GetString("autograder_user_id", "");
        if (string.IsNullOrEmpty(userIdAnon))
        {
            userIdAnon = "u_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            PlayerPrefs.SetString("autograder_user_id", userIdAnon);
        }

        sessionId = "s_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        EnsureLogHeader();
        ConfigureInputAccessibility();
    }

    private void OnMouseDown()
    {
        // Evita que el campo vuelva a aparecer cuando la puerta ya fue resuelta,
        // por ejemplo al disparar/clicar una puerta abierta.
        if (blockInteractionAfterCorrectAnswer && (questionCompleted || isDoorOpen))
            return;

        // Evita abrir varias veces el mismo input o enviar respuestas duplicadas.
        if (isInputOpen || isEvaluating)
            return;

        OpenInput();
    }

    private void OpenInput()
    {
        if (inputField == null)
        {
            Debug.LogWarning("No hay TMP_InputField asignado en el Inspector.");
            return;
        }

        if (player != null)
            player.enabled = false;

        ConfigureInputAccessibility();

        inputField.onEndEdit.RemoveListener(SubmitAnswer);
        inputField.gameObject.SetActive(true);
        inputField.text = "";
        inputField.onEndEdit.AddListener(SubmitAnswer);
        inputField.ActivateInputField();
        inputField.Select();

        isInputOpen = true;
        questionStartTime = Time.time;
    }

    private void SubmitAnswer(string answer)
    {
        if (isEvaluating)
            return;

        if (blockInteractionAfterCorrectAnswer && (questionCompleted || isDoorOpen))
        {
            CloseInput();
            return;
        }

        if (ignoreEmptyAnswers && string.IsNullOrWhiteSpace(answer))
        {
            Debug.Log("Respuesta vacía ignorada. No se registra intento.");
            CloseInput();
            return;
        }

        attemptNumber += 1;

        if (useApiEvaluation)
        {
            StartCoroutine(EvaluateWithApi(answer));
        }
        else
        {
            bool isCorrect = IsAnswerCorrect(answer);
            string label = isCorrect ? "OK" : "";
            string feedback = isCorrect ? "Respuesta correcta. Puedes avanzar." : "Respuesta incorrecta. Inténtalo de nuevo.";

            SaveAttemptLog(answer, label, isCorrect ? 1f : 0f, feedback);

            if (isCorrect)
            {
                wallText.text = answer;
                OpenDoor();
            }
            else
            {
                Debug.Log(feedback);
            }

            CloseInput();
        }
    }

    private IEnumerator EvaluateWithApi(string answer)
    {
        isEvaluating = true;

        AutoGraderRequest request = new AutoGraderRequest
        {
            item_id = itemId,
            prompt = prompt,
            answer_raw = answer,
            answer_type = answerType,
            difficulty = difficulty,
            correct_answer = correctAnswer
        };

        string json = JsonUtility.ToJson(request);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        using (UnityWebRequest req = new UnityWebRequest(evaluateUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("Auto-Grader API error: " + req.error);

                bool isCorrectFallback = IsAnswerCorrect(answer);
                string fallbackLabel = isCorrectFallback ? "OK" : "";
                SaveAttemptLog(answer, fallbackLabel, isCorrectFallback ? 1f : 0f, "API no disponible. Se usó validación exacta.");

                if (isCorrectFallback)
                {
                    wallText.text = answer;
                    OpenDoor();
                }

                isEvaluating = false;
                CloseInput();
                yield break;
            }

            AutoGraderResult result = JsonUtility.FromJson<AutoGraderResult>(req.downloadHandler.text);
            SaveAttemptLog(answer, result.label, result.confidence, result.feedback_text);

            bool canOpenDoor = result.label == "OK" && result.confidence >= apiOkMinConfidence;

            if (canOpenDoor)
            {
                wallText.text = answer;
                OpenDoor();
            }
            else
            {
                if (result.label == "OK" && result.confidence < apiOkMinConfidence)
                {
                    result.feedback_text = "La respuesta fue detectada como posible OK, pero la confianza es baja. Revisa tu respuesta e inténtalo de nuevo.";
                    Debug.LogWarning("Auto-Grader: OK con confianza baja (" + result.confidence + "). La puerta no se abre.");
                }

                Debug.Log(result.feedback_text);
            }

            isEvaluating = false;
            CloseInput();
        }
    }

    private void CloseInput()
    {
        // Se quita el listener ANTES de ocultar el input.
        // Esto evita registros vacíos cuando Unity dispara onEndEdit al desactivar el objeto.
        if (inputField != null)
        {
            inputField.onEndEdit.RemoveListener(SubmitAnswer);
            inputField.DeactivateInputField();
            inputField.gameObject.SetActive(false);
        }

        if (player != null)
            player.enabled = true;

        isInputOpen = false;
    }

    private void ConfigureInputAccessibility()
    {
        if (!applyInputAccessibilitySettings || inputField == null)
            return;

        RectTransform rect = inputField.GetComponent<RectTransform>();
        if (rect != null)
            rect.sizeDelta = inputFieldSize;

        if (inputField.textComponent != null)
        {
            inputField.textComponent.enableAutoSizing = false;
            inputField.textComponent.fontSize = inputTextFontSize;
        }

        TMP_Text placeholder = inputField.placeholder as TMP_Text;
        if (placeholder != null)
        {
            placeholder.enableAutoSizing = false;
            placeholder.fontSize = placeholderFontSize;
            placeholder.text = placeholderText;
        }

        if (singleLineInput)
            inputField.lineType = TMP_InputField.LineType.SingleLine;
    }

    private bool IsAnswerCorrect(string answer)
    {
        string normalizedAnswer = Normalize(answer);
        if (string.IsNullOrEmpty(normalizedAnswer)) return false;

        // Permite varias respuestas aceptadas separadas por "|".
        // Ejemplo en el Inspector:
        // final|Final
        // Se genera un error de compilación|se genera un error de compilacion|error de compilación
        string[] acceptedAnswers = (correctAnswer ?? "").Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string accepted in acceptedAnswers)
        {
            if (Normalize(accepted) == normalizedAnswer)
                return true;
        }

        return false;
    }

    private string Normalize(string value)
    {
        if (value == null) return "";

        string text = value.Trim().ToLowerInvariant();

        // Elimina tildes: compilación == compilacion
        string decomposed = text.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder();

        foreach (char c in decomposed)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        string withoutAccents = builder.ToString().Normalize(NormalizationForm.FormC);

        // Normaliza comillas y espacios repetidos
        withoutAccents = withoutAccents.Replace("’", "'").Replace("`", "'");
        withoutAccents = System.Text.RegularExpressions.Regex.Replace(withoutAccents, @"\s+", " ");

        return withoutAccents.Trim();
    }

    private void SaveAttemptLog(string answer, string modelLabel, float confidence, string feedbackText)
    {
        float timeSec = Time.time - questionStartTime;

        // y_label es la etiqueta real del dataset. Se deja vacío para revisión manual.
        string yLabel = "";

        string line = string.Join(",", new string[]
        {
            CsvEscape(userIdAnon),
            CsvEscape(sessionId),
            CsvEscape(DateTime.UtcNow.ToString("o")),
            CsvEscape(itemId),
            CsvEscape(prompt),
            CsvEscape(answer),
            CsvEscape(answerType),
            CsvEscape(difficulty),
            attemptNumber.ToString(),
            timeSec.ToString(System.Globalization.CultureInfo.InvariantCulture),
            hintUsed ? "1" : "0",
            CsvEscape(yLabel),
            CsvEscape(modelLabel),
            confidence.ToString(System.Globalization.CultureInfo.InvariantCulture),
            CsvEscape(feedbackText)
        });

        File.AppendAllText(GetLogPath(), line + "\n", Encoding.UTF8);
        Debug.Log("Log Auto-Grader guardado en: " + GetLogPath());
    }

    private string GetLogPath()
    {
        return Path.Combine(Application.persistentDataPath, "logs_autograder.csv");
    }

    private void EnsureLogHeader()
    {
        string path = GetLogPath();
        if (!File.Exists(path))
        {
            string header = "user_id_anon,session_id,timestamp,item_id,prompt,answer_raw,answer_type,difficulty,attempt_n,time_sec,hint_used,y_label,model_label,confidence,feedback_text";
            File.WriteAllText(path, header + "\n", Encoding.UTF8);
        }
    }

    private string CsvEscape(string value)
    {
        if (value == null) value = "";
        value = value.Replace("\"", "\"\"");
        return "\"" + value + "\"";
    }

    private void OpenDoor()
    {
        if (!isDoorOpen)
        {
            isDoorOpen = true;
            questionCompleted = true;

            if (disableColliderAfterCorrectAnswer && interactionCollider != null)
                interactionCollider.enabled = false;

            StartCoroutine(LiftDoor());
        }
    }

    private IEnumerator LiftDoor()
    {
        if (door == null)
            yield break;

        Vector3 targetPosition = doorClosedPosition + Vector3.up * doorOpenHeight;

        while (Vector3.Distance(door.position, targetPosition) > 0.01f)
        {
            door.position = Vector3.Lerp(door.position, targetPosition, doorOpenSpeed * Time.deltaTime);
            yield return null;
        }

        door.position = targetPosition;
    }

    [Serializable]
    private class AutoGraderRequest
    {
        public string item_id;
        public string prompt;
        public string answer_raw;
        public string answer_type;
        public string difficulty;
        public string correct_answer;
    }

    [Serializable]
    private class AutoGraderResult
    {
        public string label;
        public float confidence;
        public string feedback_id;
        public string feedback_text;
    }
}