package com.skeboss.minion;

import com.skeboss.modelengine.ModelEngineBridge;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;

import java.util.UUID;

public final class SkeMinion {

    private final UUID id;
    private final LivingEntity entity;
    private final String spawnerId;
    private ModelEngineBridge.BossModel model;
    private boolean ready;
    private long lastMeleeMs;

    public SkeMinion(LivingEntity entity, String spawnerId) {
        this.id = entity.getUniqueId();
        this.entity = entity;
        this.spawnerId = spawnerId;
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

    public boolean canMelee(long cooldownMs) {
        return System.currentTimeMillis() - lastMeleeMs >= cooldownMs;
    }

    public void markMelee() {
        lastMeleeMs = System.currentTimeMillis();
    }

    public Player findNearestPlayer(double range) {
        Player nearest = null;
        double best = range * range;
        for (Player player : entity.getWorld().getPlayers()) {
            if (!player.isValid() || player.isDead() || player.getGameMode().name().equals("SPECTATOR")) {
                continue;
            }
            double dist = player.getLocation().distanceSquared(entity.getLocation());
            if (dist <= best) {
                best = dist;
                nearest = player;
            }
        }
        return nearest;
    }
}
