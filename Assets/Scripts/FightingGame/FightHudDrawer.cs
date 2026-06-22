using UnityEngine;

namespace FightingGame
{
    public static class FightHudDrawer
    {
        public static void DrawSkillCooldowns(Camera camera, FighterController fighter, GUIStyle labelStyle, GUIStyle cooldownStyle)
        {
            if (camera == null || fighter == null || labelStyle == null || cooldownStyle == null)
            {
                return;
            }

            FighterArchetypeDefinition definition = FighterArchetypes.Get(fighter.ArchetypeId);
            if (!definition.HasFlameSlash && !definition.HasMoltenGuard)
            {
                return;
            }

            Vector3 worldAnchor = fighter.transform.position + new Vector3(0f, 2.05f, 0f);
            Vector3 screenPoint = camera.WorldToScreenPoint(worldAnchor);
            if (screenPoint.z < 0f)
            {
                return;
            }

            float y = Screen.height - screenPoint.y - 28f;
            float x = screenPoint.x - 42f;

            if (definition.HasFlameSlash)
            {
                DrawSkillSlot(new Rect(x, y, 84f, 22f), "U", fighter.Skill1CooldownRemaining, cooldownStyle, labelStyle);
                x += 88f;
            }

            if (definition.HasMoltenGuard)
            {
                DrawSkillSlot(new Rect(x, y, 84f, 22f), "I", fighter.Skill2CooldownRemaining, cooldownStyle, labelStyle);
            }
        }

        private static void DrawSkillSlot(Rect frame, string keyLabel, float remaining, GUIStyle cooldownStyle, GUIStyle readyStyle)
        {
            Color previous = GUI.color;
            if (remaining > 0f)
            {
                GUI.color = new Color(1f, 0.55f, 0.25f, 0.92f);
                GUI.Box(frame, GUIContent.none);
                GUI.color = previous;
                string text = keyLabel + " " + remaining.ToString("0.0") + "s";
                GUI.Label(frame, text, cooldownStyle);
                return;
            }

            GUI.color = new Color(0.35f, 0.85f, 0.45f, 0.85f);
            GUI.Box(frame, GUIContent.none);
            GUI.color = previous;
            GUI.Label(frame, keyLabel + " READY", readyStyle);
        }
    }
}
