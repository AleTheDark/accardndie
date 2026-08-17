using System;

namespace AccardND.GameCore
{
    public sealed class FlashTrialQuizSession
    {
        public const int TotalQuestions = 3;
        public int QuestionsAnswered { get; private set; }
        public int CorrectAnswers { get; private set; }
        public bool IsComplete => QuestionsAnswered >= TotalQuestions;

        public void RecordAnswer(bool correct)
        {
            if (IsComplete) throw new InvalidOperationException("La sessione quiz e' gia' completa.");
            QuestionsAnswered++;
            if (correct) CorrectAnswers++;
        }

        public FlashTrialResult Result
        {
            get
            {
                if (!IsComplete) throw new InvalidOperationException("Mancano ancora domande.");
                if (CorrectAnswers >= 3) return FlashTrialResult.Excellent;
                if (CorrectAnswers == 2) return FlashTrialResult.Good;
                return FlashTrialResult.Failed;
            }
        }
    }

    public sealed class FlashTrialQuizGame
    {
        public FlashTrialQuizGame(int correctAnswerIndex, float timeLimitSeconds = 15f)
        {
            if (correctAnswerIndex < 0 || correctAnswerIndex > 2)
                throw new ArgumentOutOfRangeException(nameof(correctAnswerIndex));
            if (timeLimitSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds));
            CorrectAnswerIndex = correctAnswerIndex;
            TimeLimitSeconds = timeLimitSeconds;
        }

        public int CorrectAnswerIndex { get; }
        public float TimeLimitSeconds { get; }
        public bool IsFinished { get; private set; }

        public FlashTrialResult Submit(int answerIndex, float elapsedSeconds)
        {
            if (IsFinished) throw new InvalidOperationException("Il quiz e' gia' terminato.");
            if (answerIndex < 0 || answerIndex > 2) throw new ArgumentOutOfRangeException(nameof(answerIndex));
            IsFinished = true;
            if (elapsedSeconds > TimeLimitSeconds || answerIndex != CorrectAnswerIndex)
                return FlashTrialResult.Failed;
            if (elapsedSeconds <= 3f) return FlashTrialResult.Perfect;
            if (elapsedSeconds <= 6f) return FlashTrialResult.Excellent;
            if (elapsedSeconds <= 10f) return FlashTrialResult.Good;
            return FlashTrialResult.Completed;
        }

        public FlashTrialResult Expire()
        {
            IsFinished = true;
            return FlashTrialResult.Failed;
        }
    }
}
