import joblib
import pandas as pd
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, confusion_matrix
from sklearn.pipeline import Pipeline
from sklearn.feature_extraction.text import TfidfVectorizer
from sklearn.linear_model import LogisticRegression

df = pd.read_csv("dataset_sintetico.csv")
df["text"] = df["prompt"].fillna("") + " [SEP] " + df["answer_raw"].fillna("")

X_train, X_test, y_train, y_test = train_test_split(
    df["text"], df["y_label"], test_size=0.25, random_state=42, stratify=df["y_label"]
)

model = Pipeline([
    ("tfidf", TfidfVectorizer(ngram_range=(1, 2), lowercase=True)),
    ("clf", LogisticRegression(max_iter=2000, class_weight="balanced")),
])

model.fit(X_train, y_train)
pred = model.predict(X_test)

print(classification_report(y_test, pred, digits=4))
print(confusion_matrix(y_test, pred, labels=sorted(df["y_label"].unique())))

joblib.dump(model, "autograder_baseline.joblib")
print("Modelo guardado en autograder_baseline.joblib")