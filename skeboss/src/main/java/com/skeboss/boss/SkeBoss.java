package com.skeboss.boss;

import com.skeboss.modelengine.ModelEngineBridge;
import org.bukkit.boss.BarColor;
import org.bukkit.boss.BarStyle;
import org.bukkit.boss.BossBar;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.scheduler.BukkitTask;

import java.util.HashMap;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

public final class SkeBoss {

    private final UUID id;
    private final LivingEntity entity;
    private ModelEngineBridge.BossModel model;
    private final BossBar bossBar;
    private final Map<String, Long> skillCooldowns = new HashMap<>();
    private final Map<UUID, Long> aggroPlayers = new ConcurrentHashMap<>();

    private boolean castingSkill;
    private String currentSkillId;
    private String currentAnimation;
    private Player target;
    private BukkitTask aimTask;

    public SkeBoss(LivingEntity entity, ModelEngineBridge.BossModel model, BossConfig config) {
        this.id = entity.getUniqueId();
        this.entity = entity;
        this.model = model;
        this.bossBar = org.bukkit.Bukkit.createBossBar(
                com.skeboss.util.TextUtil.color(config.getDisplayName()),
                BarColor.RED,
                BarStyle.SEGMENTED_10
        );
        bossBar.setProgress(1.0);
    }

    public UUID getId() {
        return id;
    }

    public LivingEntity getEntity() {
        return entity;
    }

    public ModelEngineBridge.BossModel getModel() {
        return model;
    }

    public void setModel(ModelEngineBridge.BossModel model) {
        this.model = model;
    }

    public boolean isReady() {
        return model != null;
    }

    public BossBar getBossBar() {
        return bossBar;
    }

    public boolean isCastingSkill() {
        return castingSkill;
    }

    public void setCastingSkill(boolean castingSkill) {
        this.castingSkill = castingSkill;
    }

    public String getCurrentSkillId() {
        return currentSkillId;
    }

    public void setCurrentSkillId(String currentSkillId) {
        this.currentSkillId = currentSkillId;
    }

    public Player getTarget() {
        return target;
    }

    public void setTarget(Player target) {
        this.target = target;
    }

    public String getCurrentAnimation() {
        return currentAnimation;
    }

    public void setCurrentAnimation(String currentAnimation) {
        this.currentAnimation = currentAnimation;
    }

    public BukkitTask getAimTask() {
        return aimTask;
    }

    public void setAimTask(BukkitTask aimTask) {
        this.aimTask = aimTask;
    }

    public void cancelAimTask() {
        if (aimTask != null) {
            aimTask.cancel();
            aimTask = null;
        }
    }

    public void addAggro(Player player) {
        if (player != null) {
            aggroPlayers.put(player.getUniqueId(), System.currentTimeMillis());
        }
    }

    public boolean hasAggro(Player player, long dropAfterMs) {
        if (player == null) {
            return false;
        }
        Long last = aggroPlayers.get(player.getUniqueId());
        if (last == null) {
            return false;
        }
        if (System.currentTimeMillis() - last > dropAfterMs) {
            aggroPlayers.remove(player.getUniqueId());
            return false;
        }
        return true;
    }

    public void removeAggro(Player player) {
        if (player != null) {
            aggroPlayers.remove(player.getUniqueId());
        }
    }

    public boolean isSkillReady(SkillDefinition skill) {
        Long readyAt = skillCooldowns.get(skill.id());
        return readyAt == null || System.currentTimeMillis() >= readyAt;
    }

    public void setSkillCooldown(SkillDefinition skill) {
        skillCooldowns.put(skill.id(), System.currentTimeMillis() + skill.cooldownSeconds() * 1000L);
    }

    public void updateBossBar() {
        double max = entity.getAttribute(org.bukkit.attribute.Attribute.GENERIC_MAX_HEALTH).getValue();
        bossBar.setProgress(Math.max(0.0, Math.min(1.0, entity.getHealth() / max)));
    }

    public void addViewer(Player player) {
        bossBar.addPlayer(player);
    }

    public void removeViewer(Player player) {
        bossBar.removePlayer(player);
    }

    public void removeAllViewers() {
        bossBar.removeAll();
    }
}
