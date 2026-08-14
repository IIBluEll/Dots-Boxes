// Edge 확정 요청이 거부된 원인을 호출자에게 전달하기 위한 오류 종류입니다.
namespace DotsAndBoxes.Shared
{
    public enum MOVE_ERROR_ENUM
    {
        NONE,
        INVALID_PLAYER,
        NOT_YOUR_TURN,
        INVALID_EDGE,
        EDGE_ALREADY_CONFIRMED,
        GAME_ALREADY_FINISHED
    }
}
