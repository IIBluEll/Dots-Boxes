using DotsAndBoxes.Shared;
using HM.CodeBase;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DotsAndBoxes.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Result_View : AView
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text _resultTxt;
        [SerializeField] private TMP_Text _finalScoreTxt;

        [Space(5f), Header("Buttons")]
        [SerializeField] private Button _restartBtn;

        [Space(5f), Header("Colors")]
        [SerializeField] private Color _playerOneResultColor = new Color(0.1f, 0.45f, 1f, 1f);
        [SerializeField] private Color _playerTwoResultColor = new Color(1f, 0.4f, 0.1f, 1f);
        [SerializeField] private Color _drawResultColor = Color.white;

        public event Action RestartRequested;

        private void Awake()
        {
            if ( !ValidateReferences() )
            {
                enabled = false;
                return;
            }

            _restartBtn.onClick.AddListener(OnRestartButtonClicked);
        }

        private void OnDestroy()
        {
            if ( _restartBtn != null )
            {
                _restartBtn.onClick.RemoveListener(OnRestartButtonClicked);
            }
        }

        public override void Clear()
        {
            _resultTxt.text = string.Empty;
            _finalScoreTxt.text = string.Empty;
        }

        public void ShowResult(GAME_RESULT_ENUM gameResult , int playerOneScore , int playerTwoScore)
        {
            switch ( gameResult )
            {
                case GAME_RESULT_ENUM.PLAYER_ONE_WIN:
                    _resultTxt.text = "PLAYER 1 WIN";
                    _resultTxt.color = _playerOneResultColor;
                    break;

                case GAME_RESULT_ENUM.PLAYER_TWO_WIN:
                    _resultTxt.text = "PLAYER 2 WIN";
                    _resultTxt.color = _playerTwoResultColor;
                    break;

                case GAME_RESULT_ENUM.DRAW:
                    _resultTxt.text = "DRAW";
                    _resultTxt.color = _drawResultColor;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(gameResult) , gameResult , "표시할 수 없는 게임 결과입니다.");
            }

            _finalScoreTxt.text = $"PLAYER 1  {playerOneScore} : {playerTwoScore}  PLAYER 2";
        }

        private void OnRestartButtonClicked()
        {
            RestartRequested?.Invoke();
        }

        private bool ValidateReferences()
        {
            bool isValid = _resultTxt != null &&
                           _finalScoreTxt != null &&
                           _restartBtn != null;

            if ( !isValid )
            {
                Debug.LogError("Result_View의 참조가 설정되지 않았습니다." , this);
            }

            return isValid;
        }
    }
}