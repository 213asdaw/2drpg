package com.skeboss.minion;

import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.util.TextUtil;
import org.bukkit.Bukkit;
import org.bukkit.Location;
import org.bukkit.attribute.Attribute;
import org.bukkit.boss.BossBar;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;

import java.util.UUID;

public final class SkeMinion {

    private final UUID id;
    private final LivingEntity entity;
    private final String spawnerId;
    private final MinionPreset preset;
    private final BossBar bossBar;
    private Location homeLocation;
    private ModelEngineBridge.BossModel model;
    private boolean ready;
    private boolean attacking;
    private boolean provoked;
    private UUID provokeTargetId;
    private long lastMeleeMs;

    public SkeMinion(LivingEntity entity, String spawnerId, MinionPreset preset) {
        this.id = entity.getUniqueId();
        this.entity = entity;
        this.spawnerId = spawnerId;
        this.preset = preset;
        if (preset.isBossBarEnabled()) {
            this.bossBar = Bukkit.createBossBar(
                    TextUtil.color(preset.getDisplayName()),
                    preset.getBossBarColor(),
                    preset.getBossBarStyle()
            );
            bossBar.setProgress(1.0);
        } else {
            this.bossBar = null;
        }
    }

    public UUID getId() {
        return id;
    }

    public LivingEntity getEntity() {
        return entity;
    }

    public String getSpawnerId() {
        return spawnerId;
    }

    public MinionPreset getPreset() {
        return preset;
    }

    public Location getHomeLocation() {
        return homeLocation;
    }

    public void setHomeLocation(Location homeLocation) {
        this.homeLocation = homeLocation == null ? null : homeLocation.clone();
    }

    public BossBar getBossBar() {
        return bossBar;
    }

    public boolean hasBossBar() {
        return bossBar != null;
    }

    public void updateBossBar() {
        if (bossBar == null) {
            return;
        }
        var maxAttr = entity.getAttribute(Attribute.GENERIC_MAX_HEALTH);
        if (maxAttr == null) {
            return;
        }
        double max = maxAttr.getValue();
        bossBar.setProgress(Math.max(0.0, Math.min(1.0, entity.getHealth() / max)));
    }

    public void addViewer(Player player) {
        if (bossBar != null && player != null) {
            bossBar.addPlayer(player);
        }
    }

    public void removeViewer(Player player) {
        if (bossBar != null && player != null) {
            bossBar.removePlayer(player);
        }
    }

    public void removeAllViewers() {
        if (bossBar != null) {
            bossBar.removeAll();
        }
    }

    public ModelEngineBridge.BossModel getModel() {
        return model;
    }

    public void setModel(ModelEngineBridge.BossModel model) {
        this.model = model;
    }

    public boolean isReady() {
        return ready;
    }

    public void setReady(boolean ready) {
        this.ready = ready;
    }

    public boolean isAttacking() {
        return attacking;
    }

    public void setAttacking(boolean attacking) {
        this.attacking = attacking;
    }

    public boolean isProvoked() {
        return provoked;
    }

    public void provoke(Player player) {
        provoked = true;
        if (player != null) {
            provokeTargetId = player.getUniqueId();
        }
    }

    public Player getProvokeTarget() {
        if (provokeTargetId == null) {
            return null;
        }
        Player player = Bukkit.getPlayer(provokeTargetId);
        if (player == null || !player.isValid() || player.isDead()) {
            return null;
        }
        return player;
    }

    public boolean canMelee(long cooldownMs) {
        return System.currentTimeMillis() - lastMeleeMs >= cooldownMs;
    }

    public void markMelee() {
        lastMeleeMs = System.currentTimeMillis();
    }

    public Player findNearestPlayer(double range) {
        return findNearestPlayerNear(entity.getLocation(), range);
    }

    public Player findNearestPlayerNear(Location center, double range) {
        if (center == null || center.getWorld() == null) {
            return null;
        }
        Player nearest = null;
        double best = range * range;
        for (Player player : center.getWorld().getPlayers()) {
            if (!player.isValid() || player.isDead() || player.getGameMode().name().equals("SPECTATOR")) {
                continue;
            }
            double dist = player.getLocation().distanceSquared(center);
            if (dist <= best) {
                best = dist;
                nearest = player;
            }
        }
        return nearest;
    }
}
