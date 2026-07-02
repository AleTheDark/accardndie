using System;
using System.Collections.Generic;
using UnityEngine;

namespace AccardND.GameData
{
    [CreateAssetMenu(menuName = "Accard N' Die/Scenario Catalog", fileName = "ScenarioCatalog")]
    public sealed class ScenarioCatalog : ScriptableObject
    {
        [SerializeField] private ScenarioDefinition[] scenarios = Array.Empty<ScenarioDefinition>();

        public IReadOnlyList<ScenarioDefinition> Scenarios => scenarios;

        public ScenarioDefinition Select(
            RoomType roomType,
            RoomDifficulty difficulty,
            string bossId = null,
            string scenarioId = null)
        {
            if (!string.IsNullOrWhiteSpace(scenarioId))
            {
                ScenarioDefinition explicitScenario = FindById(scenarioId);
                if (explicitScenario != null
                    && (roomType == RoomType.Any
                        || explicitScenario.RoomType == roomType
                        || explicitScenario.RoomType == RoomType.Any))
                {
                    return explicitScenario;
                }
            }

            if (!string.IsNullOrWhiteSpace(bossId))
            {
                foreach (ScenarioDefinition scenario in scenarios)
                {
                    if (scenario != null
                        && string.Equals(scenario.BossId, bossId, StringComparison.OrdinalIgnoreCase))
                    {
                        return scenario;
                    }
                }
            }

            foreach (ScenarioDefinition scenario in scenarios)
            {
                if (scenario != null
                    && scenario.RoomType == roomType
                    && scenario.Difficulty == difficulty)
                {
                    return scenario;
                }
            }

            foreach (ScenarioDefinition scenario in scenarios)
            {
                if (scenario != null
                    && scenario.RoomType == roomType
                    && scenario.Difficulty == RoomDifficulty.Any)
                {
                    return scenario;
                }
            }

            return FindById("default");
        }

        public ScenarioDefinition FindById(string id)
        {
            foreach (ScenarioDefinition scenario in scenarios)
            {
                if (scenario != null && string.Equals(scenario.Id, id, StringComparison.OrdinalIgnoreCase))
                    return scenario;
            }

            return null;
        }

#if UNITY_EDITOR
        public void SetScenarios(ScenarioDefinition[] definitions)
        {
            scenarios = definitions ?? Array.Empty<ScenarioDefinition>();
        }
#endif
    }
}
