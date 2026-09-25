using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Tests
{
    /// <summary>
    /// Quest progression across multiple objectives and nodes, driving a real QuestInstance with
    /// authored graph data plus quest events, without a scene or QuestManager.
    ///
    /// Unity setup: none. Runs in EditMode.
    /// </summary>
    public class QuestProgressionTests
    {
        private static QuestGraphData BuildGraph(int requiredCount)
        {
            return new QuestGraphData
            {
                questId = "test_quest",
                startNodeId = "start",
                nodes = new List<QuestNodeData>
                {
                    new QuestNodeData
                    {
                        id = "start",
                        objectives = new List<QuestObjectiveData>
                        {
                            new QuestObjectiveData
                            {
                                id = "kill",
                                eventType = "EnemyKilled",
                                targetId = "goblin",
                                requiredCount = requiredCount,
                            },
                        },
                        transitions = new List<QuestTransitionData>
                        {
                            new QuestTransitionData
                            {
                                targetNodeId = "done",
                                automatic = true,
                                conditions = new List<QuestConditionData>
                                {
                                    new QuestConditionData { type = "ObjectiveComplete", objectiveId = "kill" },
                                },
                            },
                        },
                    },
                    new QuestNodeData { id = "done" },
                },
            };
        }

        [Test]
        public void Events_Count_Objectives_And_Must_Match_The_Target()
        {
            QuestInstance quest = new QuestInstance(BuildGraph(2));

            quest.OnEvent("EnemyKilled", "bandit", 1);
            Assert.IsFalse(quest.ObjectiveCounts.ContainsKey("kill"));

            quest.OnEvent("EnemyKilled", "goblin", 1);
            Assert.AreEqual(1, quest.ObjectiveCounts["kill"]);
            Assert.IsFalse(quest.IsObjectiveComplete("kill"));
            Assert.IsTrue(quest.IsInNode("start"));

            // OnEvent auto-advances: completing the objective moves the node automatically.
            quest.OnEvent("EnemyKilled", "goblin", 1);
            Assert.AreEqual(2, quest.ObjectiveCounts["kill"]);
            Assert.IsTrue(quest.IsInNode("done"));
        }

        [Test]
        public void Excess_Events_Clamp_To_The_Required_Count()
        {
            QuestInstance quest = new QuestInstance(BuildGraph(2));

            quest.OnEvent("EnemyKilled", "goblin", 99);

            Assert.AreEqual(2, quest.ObjectiveCounts["kill"]);
        }

        [Test]
        public void Automatic_Transition_Waits_Until_The_Objective_Completes()
        {
            QuestInstance quest = new QuestInstance(BuildGraph(1));

            quest.TryAdvance();
            Assert.IsTrue(quest.IsInNode("start"));

            quest.OnEvent("EnemyKilled", "goblin", 1);
            quest.TryAdvance();
            Assert.IsTrue(quest.IsInNode("done"));
        }

        [Test]
        public void Manual_Transitions_Require_An_Explicit_Choice()
        {
            QuestGraphData graph = BuildGraph(2);
            QuestNodeData start = graph.nodes[0];
            start.transitions = new List<QuestTransitionData>
            {
                new QuestTransitionData
                {
                    targetNodeId = "done",
                    automatic = false,
                    conditions = new List<QuestConditionData>
                    {
                        new QuestConditionData { type = "ObjectiveComplete", objectiveId = "kill" },
                    },
                },
            };

            QuestInstance quest = new QuestInstance(graph);

            quest.TryChooseTransition("done");
            Assert.IsTrue(quest.IsInNode("start"), "incomplete objectives must block the transition");

            quest.OnEvent("EnemyKilled", "goblin", 2);
            quest.TryChooseTransition("done");
            Assert.IsTrue(quest.IsInNode("done"));
        }

        [Test]
        public void Wildcard_Targets_Count_Any_Event()
        {
            QuestGraphData graph = BuildGraph(1);
            QuestNodeData start = graph.nodes[0];
            start.objectives = new List<QuestObjectiveData>
            {
                new QuestObjectiveData { id = "any_kill", eventType = "EnemyKilled", targetId = "*", requiredCount = 1 },
            };
            start.transitions = new List<QuestTransitionData>
            {
                new QuestTransitionData
                {
                    targetNodeId = "done",
                    automatic = true,
                    conditions = new List<QuestConditionData>
                    {
                        new QuestConditionData { type = "ObjectiveComplete", objectiveId = "any_kill" },
                    },
                },
            };

            QuestInstance quest = new QuestInstance(graph);
            quest.OnEvent("EnemyKilled", "whoever", 1);

            Assert.AreEqual(1, quest.ObjectiveCounts["any_kill"]);
            Assert.IsTrue(quest.IsInNode("done"));
        }

        [Test]
        public void Deferred_Instance_Does_Not_Enter_The_Start_Node_Until_Begin()
        {
            QuestInstance quest = QuestInstance.Deferred(BuildGraph(1));

            Assert.AreEqual(0, quest.ActiveNodeIds.Count,
                "a deferred instance must not run initial node actions before it is registered");

            quest.Begin();

            Assert.IsTrue(quest.IsInNode("start"), "Begin must enter the start node");
        }

        [Test]
        public void Begin_Is_Idempotent()
        {
            QuestInstance quest = QuestInstance.Deferred(BuildGraph(1));

            quest.Begin();
            quest.Begin();

            Assert.AreEqual(1, quest.ActiveNodeIds.Count, "Begin must enter the start node exactly once");
        }

        [Test]
        public void Reaching_A_Terminal_Node_Notifies_Quest_Completion()
        {
            var completed = new List<string>();
            System.Action<string> original = QuestRuntimeBindings.MarkQuestCompleted;
            QuestRuntimeBindings.MarkQuestCompleted = id => completed.Add(id);
            try
            {
                QuestInstance quest = new QuestInstance(BuildGraph(1));
                quest.OnEvent("EnemyKilled", "goblin", 1); // objective -> "done" (terminal)

                Assert.AreEqual(1, completed.Count, "a terminal node must fire the completion hook once");
                Assert.AreEqual("test_quest", completed[0]);
            }
            finally
            {
                QuestRuntimeBindings.MarkQuestCompleted = original;
            }
        }
    }
}
