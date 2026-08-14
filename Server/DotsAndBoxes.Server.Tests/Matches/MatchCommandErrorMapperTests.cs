using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;
using NUnit.Framework;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchCommandErrorMapperTests
    {
        [TestCase(MOVE_ERROR_ENUM.NONE , MATCH_COMMAND_ERROR_ENUM.NONE)]
        [TestCase(MOVE_ERROR_ENUM.INVALID_PLAYER , MATCH_COMMAND_ERROR_ENUM.NOT_A_MATCH_PLAYER)]
        [TestCase(MOVE_ERROR_ENUM.NOT_YOUR_TURN , MATCH_COMMAND_ERROR_ENUM.NOT_YOUR_TURN)]
        [TestCase(MOVE_ERROR_ENUM.INVALID_EDGE , MATCH_COMMAND_ERROR_ENUM.INVALID_EDGE)]
        [TestCase(MOVE_ERROR_ENUM.EDGE_ALREADY_CONFIRMED , MATCH_COMMAND_ERROR_ENUM.EDGE_ALREADY_CONFIRMED)]
        [TestCase(MOVE_ERROR_ENUM.GAME_ALREADY_FINISHED , MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_ACTIVE)]
        public void ToMatchCommandError_MapsCoreError(
            MOVE_ERROR_ENUM moveError ,
            MATCH_COMMAND_ERROR_ENUM expectedError)
        {
            MATCH_COMMAND_ERROR_ENUM result =
                MatchCommandErrorMapper.ToMatchCommandError(moveError);

            Assert.That(result , Is.EqualTo(expectedError));
        }

        [Test]
        public void ToMatchCommandError_WithUnknownValue_ReturnsInternalError()
        {
            MOVE_ERROR_ENUM unknownError = (MOVE_ERROR_ENUM)999;

            MATCH_COMMAND_ERROR_ENUM result =
                MatchCommandErrorMapper.ToMatchCommandError(unknownError);

            Assert.That(result , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.INTERNAL_ERROR));
        }
    }
}