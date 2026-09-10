using UnityEngine;

namespace DotsAndBoxes.UI
{
    public sealed class BootStrap_model
    {
        private const float MIN_PROGRESS = 0f;
        private const float MAX_PROGRESS = 100f;

        public float Progress { get; private set; }
        public string StatusMessage { get; private set; }
        public string ErrorMessage { get; private set; }

        public bool IsLoading { get; private set; }
        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public BootStrap_model()
        {
            Reset();
        }

        public void Reset()
        {
            Progress = MIN_PROGRESS;
            StatusMessage = "게임을 준비하고 있습니다....";
            ErrorMessage = string.Empty;
            IsLoading = false;
        }

        public void BeginLoading()
        {
            Progress = MIN_PROGRESS;
            StatusMessage = "게임을 준비하고 있습니다....";
            ErrorMessage = string.Empty;
            IsLoading = true;
        }

        public void UpdateLoading(float progress, string statusMessage)
        {
            Progress = ClampProgress(progress);

            if(!string.IsNullOrWhiteSpace(statusMessage))
            {
                StatusMessage = statusMessage;
            }

            ErrorMessage = string.Empty;
            IsLoading = true;
        }

        public void Complete(string statusMessage)
        {
            Progress = MAX_PROGRESS;
            StatusMessage = string.IsNullOrWhiteSpace(statusMessage) ? "준비가 완료되었습니다." : statusMessage;

            ErrorMessage = string.Empty;
            IsLoading = false;
        }

        public void Fail(string errorMessage)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(errorMessage) ? "게임을 준비하지 못했습니다." : errorMessage;
            StatusMessage = "로그인에 실패했습니다.";
            IsLoading = false;
        }

        private static float ClampProgress(float progress)
        {
            if(progress < MIN_PROGRESS)
            {
                return MIN_PROGRESS;
            }

            if(progress > MAX_PROGRESS)
            {
                return MAX_PROGRESS;
            }

            return progress;
        }
    }
}

