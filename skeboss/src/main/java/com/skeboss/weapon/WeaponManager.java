package com.skeboss.weapon;

import com.skeboss.SkeBossPlugin;
import com.skeboss.boss.BossConfig;
import com.skeboss.boss.BossManager;
import com.skeboss.boss.SkillDefinition;
import com.skeboss.skill.PlayerChainSkill;
import com.skeboss.skill.PlayerLaserSkill;
import com.skeboss.skript.SkriptBridge;
import com.skeboss.util.TextUtil;
import org.bukkit.NamespacedKey;
import org.bukkit.entity.Player;
import org.bukkit.inventory.ItemFlag;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;
import org.bukkit.persistence.PersistentDataType;

import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

public final class WeaponManager {

    public static final String USING_SKILL_TAG = "using_skill";

    private final SkeBossPlugin plugin;
    private final BossManager bossManager;
    private final SkriptBridge skriptBridge;
    private final NamespacedKey weaponKey;
    private WeaponConfig weaponConfig;
    private final Map<UUID, Map<String, Long>> cooldowns = new HashMap<>();

    public WeaponManager(SkeBossPlugin plugin, BossManager bossManager, SkriptBridge skriptBridge) {
        this.plugin = plugin;
        this.bossManager = bossManager;
        this.skriptBridge = skriptBridge;
        this.weaponKey = new NamespacedKey(plugin, "weapon_id");
        reload();
    }

    public void reload() {
        weaponConfig = new WeaponConfig(plugin);
    }

    public WeaponConfig getWeaponConfig() {
        return weaponConfig;
    }

    public ItemStack createWeapon(String weaponId) {
        WeaponDefinition weapon = weaponConfig.getWeapon(weaponId);
        if (weapon == null) {
            return null;
        }

        ItemStack item = new ItemStack(weapon.material());
        ItemMeta meta = item.getItemMeta();
        if (meta == null) {
            return item;
        }

        meta.setDisplayName(TextUtil.color(weapon.displayName()));
        List<String> lore = weapon.lore().stream().map(TextUtil::color).toList();
        lore = new java.util.ArrayList<>(lore);
        lore.add(TextUtil.color("&8무기: " + weapon.id()));
        lore.add(TextUtil.color("&7쿨타임: &f" + weapon.cooldownSeconds() + "초"));
        meta.setLore(lore);
        meta.addItemFlags(ItemFlag.HIDE_ATTRIBUTES);
        meta.getPersistentDataContainer().set(weaponKey, PersistentDataType.STRING, weapon.id());
        item.setItemMeta(meta);
        return item;
    }

    public String getWeaponId(ItemStack item) {
        if (item == null || !item.hasItemMeta()) {
            return null;
        }
        return item.getItemMeta().getPersistentDataContainer().get(weaponKey, PersistentDataType.STRING);
    }

    public boolean isWeapon(ItemStack item) {
        return getWeaponId(item) != null;
    }

    public boolean tryUse(Player player, ItemStack item) {
        String weaponId = getWeaponId(item);
        if (weaponId == null) {
            return false;
        }

        WeaponDefinition weapon = weaponConfig.getWeapon(weaponId);
        if (weapon == null) {
            return false;
        }

        if (!player.hasPermission("skeboss.weapon.use")) {
            player.sendMessage(TextUtil.color("&c무기 사용 권한이 없습니다."));
            return true;
        }

        if (isOnCooldown(player, weaponId)) {
            long left = cooldownLeftSeconds(player, weaponId);
            player.sendMessage(TextUtil.color("&c쿨타임 &f" + left + "초"));
            return true;
        }

        SkillDefinition skill = findSkill(weapon.skillId());
        if (skill == null) {
            player.sendMessage(TextUtil.color("&c스킬 설정 없음: &f" + weapon.skillId()));
            return true;
        }

        boolean cast;
        if (skill.isBeamSkill()) {
            cast = PlayerLaserSkill.execute(plugin, this, player, skill);
        } else if (skill.isChainSkill()) {
            cast = PlayerChainSkill.execute(plugin, this, player, skill);
        } else {
            player.sendMessage(TextUtil.color("&c지원하지 않는 스킬: &f" + skill.id()));
            return true;
        }

        if (cast) {
            setCooldown(player, weaponId, weapon.cooldownSeconds());
            player.addScoreboardTag(USING_SKILL_TAG);
            int skillDuration = skill.durationTicks();
            plugin.getServer().getScheduler().runTaskLater(
                    plugin,
                    () -> player.removeScoreboardTag(USING_SKILL_TAG),
                    skillDuration
            );
        }
        return true;
    }

    private SkillDefinition findSkill(String skillId) {
        return bossManager.getConfig().getSkills().stream()
                .filter(skill -> skill.id().equalsIgnoreCase(skillId))
                .findFirst()
                .orElse(null);
    }

    public double getPlayerBeamDamage(Player player) {
        BossConfig config = bossManager.getConfig();
        double attack = skriptBridge.getEntityStat(
                player,
                config.getSkriptAttackVariable(),
                config.getFallbackAttackStat()
        );
        return Math.max(0.0, attack * config.getBeamAttackMultiplier());
    }

    public double getPlayerSkillDamage(Player player, SkillDefinition skill) {
        double attack = skriptBridge.getEntityStat(
                player,
                bossManager.getConfig().getSkriptAttackVariable(),
                0.0
        );
        return Math.max(skill.damage(), skill.damage() + attack * 0.25);
    }

    public boolean canTarget(Player caster, org.bukkit.entity.LivingEntity target) {
        if (target.equals(caster) || !target.isValid() || target.isDead()) {
            return false;
        }
        if (target instanceof Player) {
            return weaponConfig.isPvp();
        }
        if (bossManager.isBoss(target)) {
            return weaponConfig.isTargetBoss();
        }
        return weaponConfig.isTargetMobs();
    }

    public BossManager getBossManager() {
        return bossManager;
    }

    private boolean isOnCooldown(Player player, String weaponId) {
        return cooldownLeftMs(player, weaponId) > 0;
    }

    private long cooldownLeftSeconds(Player player, String weaponId) {
        return (cooldownLeftMs(player, weaponId) + 999) / 1000;
    }

    private long cooldownLeftMs(Player player, String weaponId) {
        Map<String, Long> map = cooldowns.get(player.getUniqueId());
        if (map == null) {
            return 0;
        }
        Long readyAt = map.get(weaponId);
        if (readyAt == null) {
            return 0;
        }
        return Math.max(0, readyAt - System.currentTimeMillis());
    }

    private void setCooldown(Player player, String weaponId, int seconds) {
        cooldowns.computeIfAbsent(player.getUniqueId(), id -> new HashMap<>())
                .put(weaponId, System.currentTimeMillis() + seconds * 1000L);
    }
}
