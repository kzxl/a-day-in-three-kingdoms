using SimT.Contracts.Scenes;

namespace SimT.Modules.Dlc.Character;

internal sealed partial class DialogueService
{
    private static TalkScene WithFeedbackExpression(TalkScene scene, string expression)
    {
        // Match the actual feedback line: introductions may have been inserted before it.
        var expressions = Enumerable.Repeat("normal", scene.Lines.Count).ToArray();
        for (var i = 0; i < scene.Lines.Count; i++)
            if (scene.Lines[i] == scene.FeedbackVi) expressions[i] = expression;
        return scene with { LineExpressions = expressions };
    }
}
