using DotsAndBoxes.Shared;
using HM.CodeBase;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace DotsAndBoxes.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Result_View : AView
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text _resultTxt;
        [SerializeField] private TMP_Text _finalScoreTxt;

        [Space(5f), Header("Result Image")]
        [SerializeField] private Image _resultImg;
        [SerializeField] private Sprite _winSprite;
        [SerializeField] private Sprite _loseSprite;
        [SerializeField] private Sprite _drawSprite;

        [Space(5f), Header("Buttons")]
        [SerializeField] private Button _lobbyBtn;

        [Space(5f), Header("Colors")]
        [FormerlySerializedAs("_playerOneResultColor")]
        [SerializeField] private Color _winResultColor = new Color(1f, 0.72f, 0.12f, 1f);
        [FormerlySerializedAs("_playerTwoResultColor")]
        [SerializeField] private Color _loseResultColor = new Color(1f, 0.3f, 0.25f, 1f);
        [SerializeField] private Color _drawResultColor = Color.white;

        public event Action LobbyRequested;

        private void Awake()
        {
            if ( !ValidateReferences() )
            {
                enabled = false;
                return;
            }

            _lobbyBtn.onClick.AddListener(OnLobbyButtonClicked);
        }

        private void OnDestroy()
        {
            if ( _lobbyBtn != null )
            {
                _lobbyBtn.onClick.RemoveListener(OnLobbyButtonClicked);
            }
        }

        public override void Clear()
        {
            _resultTxt.text = string.Empty;
            _finalScoreTxt.text = string.Empty;
            ShowResultImage(null);
        }

        public void ShowResult(
            LOCAL_GAME_RESULT_ENUM localGameResult ,
            int localPlayerScore ,
            int opponentScore)
        {
            switch ( localGameResult )
            {
                case LOCAL_GAME_RESULT_ENUM.WIN:
                    _resultTxt.text = "승리!";
                    _resultTxt.color = _winResultColor;
                    ShowResultImage(_winSprite);
                    break;

                case LOCAL_GAME_RESULT_ENUM.LOSE:
                    _resultTxt.text = "패배";
                    _resultTxt.color = _loseResultColor;
                    ShowResultImage(_loseSprite);
                    break;

                case LOCAL_GAME_RESULT_ENUM.DRAW:
                    _resultTxt.text = "무승부";
                    _resultTxt.color = _drawResultColor;
                    ShowResultImage(_drawSprite);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(localGameResult) , localGameResult , "표시할 수 없는 게임 결과입니다.");
            }

            _finalScoreTxt.text = $"{localPlayerScore} : {opponentScore}";
        }

        public void ShowMessage(string title , string message)
        {
            ShowResultImage(null);
            _resultTxt.text = string.IsNullOrWhiteSpace(title) ? "NOTICE" : title;
            _resultTxt.color = _drawResultColor;
            _finalScoreTxt.text = string.IsNullOrWhiteSpace(message) ? "확인 후 Lobby로 이동해 주세요." : message;
        }

        private void OnLobbyButtonClicked()
        {
            LobbyRequested?.Invoke();
        }

        private void ShowResultImage(Sprite resultSprite)
        {
            if ( _resultImg == null )
            {
                return;
            }

            _resultImg.sprite = resultSprite;
            _resultImg.gameObject.SetActive(resultSprite != null);
        }

        private bool ValidateReferences()
        {
            bool isValid = _resultTxt != null &&
               _finalScoreTxt != null &&
               _lobbyBtn != null;

            if ( !isValid )
            {
                Debug.LogError("Result_View의 참조가 설정되지 않았습니다." , this);
            }

            return isValid;
        }
    }
}
