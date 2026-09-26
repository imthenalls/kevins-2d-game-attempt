/// <summary>
/// Evaluates the optional quest gating on dialogue nodes and choices. A node or choice is available
/// when any required quest is active and, when a quest node is named, that quest is currently sitting
/// on it. Entries with no gating are always available.
///
/// Unity setup: none — static helper used by NpcDialogue (start-node selection) and
/// PlayerInteractionController (choice filtering).
/// </summary>
public static class DialogueGate
{
    public static bool IsAvailable(string requireQuestId, string requireQuestNodeId)
    {
        bool needsQuest = !string.IsNullOrWhiteSpace(requireQuestId);
        bool needsNode = !string.IsNullOrWhiteSpace(requireQuestNodeId);

        if (!needsQuest && !needsNode)
            return true;

        QuestManager manager = QuestManager.Instance;
        if (manager == null)
            return false;

        if (needsQuest && !manager.IsQuestActive(requireQuestId))
            return false;

        if (needsNode && !manager.IsQuestInNode(requireQuestId, requireQuestNodeId))
            return false;

        return true;
    }
}
