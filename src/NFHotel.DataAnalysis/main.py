import sys

import lmstudio as lms
import pandas as pd

# Constants
DATA_FILE_PATH = "data/nf_hotel_bookings.csv"

llm = lms.llm("qwen/qwen3.8-27b")


def load_data() -> pd.DataFrame:
    df = pd.read_csv(DATA_FILE_PATH, sep=";", low_memory=False)
    return df


def descriptive_analysis(df: pd.DataFrame) -> str:
    result = df.drop(columns="booking_id").describe().T
    msg = (
        "You work as a data analyst and in marketing to optimize hotel operations. We have extract descriptive analysis: "
        + result
    )
    respond = llm.respond(msg)
    return respond


def ia_conclusion(msg: str) -> str:
    conclusion = "For NF Hotel we need some conlusion for following: " + msg
    return conclusion


def run():
    df = load_data()
    print(df.drop(columns="booking_id").describe().T)
    result = llm.respond("What is the meaning of life?")
    print(result)


# Block direct execution
if __name__ == "__main__":
    print("❌ Error: This script cannot be executed directly.", file=sys.stderr)
    print("Please use command `./run.py`", file=sys.stderr)
    sys.exit(1)
