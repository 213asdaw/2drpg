package com.skeboss.weapon;

import org.bukkit.Material;

import java.util.List;

public record WeaponDefinition(
        String id,
        Material material,
        String displayName,
        List<String> lore,
        String skillId,
        String sneakSkillId,
        int cooldownSeconds
) {
}
