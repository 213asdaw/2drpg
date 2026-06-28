package com.skeboss.minion;

import com.skeboss.SkeBossPlugin;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.util.TextUtil;
import org.bukkit.Bukkit;
import org.bukkit.Location;
import org.bukkit.attribute.Attribute;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.entity.Zombie;
import org.bukkit.metadata.FixedMetadataValue;
import org.bukkit.scheduler.BukkitTask;

import java.util.Collection;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.logging.Level;

public final class MinionManager {

    public static final String METADATA_KEY = "skeboss_minion";
    public static final String SCOREBOARD_TAG = "skeboss_minion";

    private final SkeBossPlugin plugin;
    private final ModelEngineBridge modelEngine;
    private final MinionSpawnerStorage spawnerStorage;
    private final Map<UUID, SkeMinion> minions = new ConcurrentHashMap<>();
    private final Map<String, BukkitTask> respawnTasks = new ConcurrentHashMap<>();
    private MinionConfig config;

    public MinionManager(SkeBossPlugin plugin, ModelEngineBridge modelEngine) {
        this.plugin = plugin;
        this.modelEngine = modelEngine;
        this.config = new MinionConfig(plugin);
        this.spawnerStorage = new MinionSpawnerStorage(plugin);
        spawnerStorage.load();
    }

    public MinionConfig getConfig() {
        return config;
    }

    public ModelEngineBridge getModelEngine() {
        return modelEngine;
    }

    public MinionSpawnerStorage getSpawnerStorage() {
        return spawnerStorage;
    }

    public void reload() {
        config = new MinionConfig(plugin);
        spawnerStorage.load();
    }

    public void startupSpawners() {
        for (MinionSpawner spawner : spawnerStorage.all()) {
            Location location = spawner.toLocation();
            if (location == null) {
                plugin.getLogger().warning("스포너 월드 없음: " + spawner.getId());
                continue;
            }
            spawnForSpawner(spawner);
        }
    }

    public MinionSpawner createSpawner(String id, Location location) {
        if (spawnerStorage.get(id) != null) {
            throw new IllegalStateException("이미 있는 스포너 ID: " + id);
        }
        MinionSpawner spawner = spawnerStorage.put(new MinionSpawner(id, location));
        spawnForSpawner(spawner);
        return spawner;
    }

    public boolean removeSpawner(String id) {
        cancelRespawn(id);
        MinionSpawner spawner = spawnerStorage.get(id);
        if (spawner == null) {
            return false;
        }
        if (spawner.getActiveMinionId() != null) {
            SkeMinion minion = minions.get(spawner.getActiveMinionId());
            if (minion != null) {
                remove(minion);
            }
        }
        return spawnerStorage.remove(id);
    }

    public SkeMinion spawnForSpawner(MinionSpawner spawner) {
        if (spawner.getActiveMinionId() != null) {
            SkeMinion existing = minions.get(spawner.getActiveMinionId());
            if (existing != null && existing.getEntity().isValid() && !existing.getEntity().isDead()) {
                return existing;
            }
        }
        Location location = spawner.toLocation();
        if (location == null) {
            throw new IllegalStateException("월드를 찾을 수 없습니다.");
        }
        SkeMinion minion = spawnAt(location, spawner.getId());
        spawner.setActiveMinionId(minion.getId());
        spawnerStorage.save();
        return minion;
    }

    public SkeMinion spawnAt(Location location, String spawnerId) {
        Location spawnLoc = location.clone();

        Zombie zombie = location.getWorld().spawn(spawnLoc, Zombie.class, entity -> {
            entity.setBaby(false);
            entity.setSilent(true);
            entity.setCanPickupItems(false);
            entity.setRemoveWhenFarAway(false);
            entity.setShouldBurnInDay(false);
            entity.setFireTicks(0);
            entity.setCustomNameVisible(true);
            entity.setCustomName(TextUtil.color(config.getDisplayName()));
            entity.setMetadata(METADATA_KEY, new FixedMetadataValue(plugin, true));
            entity.addScoreboardTag(SCOREBOARD_TAG);

            Attribute maxHealth = Attribute.GENERIC_MAX_HEALTH;
            if (entity.getAttribute(maxHealth) != null) {
                entity.getAttribute(maxHealth).setBaseValue(config.getMaxHealth());
            }
            entity.setHealth(config.getMaxHealth());

            Attribute speed = Attribute.GENERIC_MOVEMENT_SPEED;
            if (entity.getAttribute(speed) != null) {
                entity.getAttribute(speed).setBaseValue(config.getMovementSpeed());
            }

            Attribute follow = Attribute.GENERIC_FOLLOW_RANGE;
            if (entity.getAttribute(follow) != null) {
                entity.getAttribute(follow).setBaseValue(config.getFollowRange());
            }
        });

        SkeMinion minion = new SkeMinion(zombie, spawnerId, config);
        minions.put(zombie.getUniqueId(), minion);

        int delay = Math.max(1, config.getSpawnDelayTicks());
        Bukkit.getScheduler().runTaskLater(plugin, () -> finishSpawn(minion), delay);
        return minion;
    }

    private void finishSpawn(SkeMinion minion) {
        LivingEntity entity = minion.getEntity();
        if (!entity.isValid() || entity.isDead()) {
            minions.remove(entity.getUniqueId());
            return;
        }

        String resolvedModelId = modelEngine.resolveAvailableModelId(
                config.getModelId(), config.getModelFallbackIds());
        if (resolvedModelId == null) {
            plugin.getLogger().warning("잡몹 blueprint 없음 — 좀비만 표시합니다. /skeboss minion check");
            registerBossBarViewers(minion);
            minion.setReady(true);
            return;
        }

        try {
            ModelEngineBridge.BossModel model = modelEngine.attachMinionModel(
                    entity,
                    config.getModelId(),
                    config.getModelFallbackIds(),
                    config.getModelScale(),
                    config.getHitboxScale()
            );

            int limbBones = modelEngine.countPlayerLimbs(model);
            if (limbBones == 0) {
                plugin.getLogger().warning("잡몹 PlayerLimb 탐지 0개 (" + resolvedModelId
                        + ") — 스킨 적용은 계속 시도합니다.");
            }

            minion.setModel(model);
            double syncRadius = config.getViewerSyncRadius();
            modelEngine.applyPlayerSkin(model, entity, config.getSkinUsername(), syncRadius, appliedLimbs -> {
                if (appliedLimbs >= 6 && config.isHideBaseEntity()) {
                    Bukkit.getScheduler().runTaskLater(plugin, () -> {
                        if (!entity.isValid() || entity.isDead()) {
                            return;
                        }
                        modelEngine.setBaseEntityVisible(model, entity, false, syncRadius);
                        modelEngine.forceResyncNearbyPlayers(model, entity, syncRadius);
                    }, 20L);
                } else if (appliedLimbs > 0 && appliedLimbs < 6) {
                    modelEngine.restoreBaseEntityVisibility(entity);
                    plugin.getLogger().warning("잡몹 스킨 일부만 적용 (" + appliedLimbs
                            + "개) — 좀비 본체 유지");
                } else if (appliedLimbs == 0) {
                    modelEngine.restoreBaseEntityVisibility(entity);
                    plugin.getLogger().warning("잡몹 스킨 미적용 — 좀비 본체를 유지합니다.");
                }
            });
            registerBossBarViewers(minion);
            minion.setReady(true);
            plugin.getLogger().info("잡몹 스폰: " + config.getSkinUsername() + " (모델: " + resolvedModelId
                    + ", PlayerLimb " + limbBones + "개) @ " + entity.getLocation());
        } catch (RuntimeException ex) {
            plugin.getLogger().log(Level.SEVERE, "잡몹 모델 적용 실패 — 좀비만 사용 (/meg reload, model-id: "
                    + config.getModelId() + ")", ex);
            modelEngine.restoreBaseEntityVisibility(entity);
            registerBossBarViewers(minion);
            minion.setReady(true);
        }
    }

    private void registerBossBarViewers(SkeMinion minion) {
        if (!minion.hasBossBar()) {
            return;
        }
        double radiusSq = config.getViewerSyncRadius() * config.getViewerSyncRadius();
        LivingEntity entity = minion.getEntity();
        for (Player player : Bukkit.getOnlinePlayers()) {
            if (!player.getWorld().equals(entity.getWorld())) {
                continue;
            }
            if (player.getLocation().distanceSquared(entity.getLocation()) <= radiusSq) {
                minion.addViewer(player);
            }
        }
    }

    public void syncMinionViewers(SkeMinion minion, Player player) {
        if (minion.getModel() != null) {
            modelEngine.syncNearbyPlayers(minion.getModel(), minion.getEntity(), config.getViewerSyncRadius());
        }
        minion.addViewer(player);
    }

    public void syncBossBarViewers(SkeMinion minion) {
        if (!minion.hasBossBar()) {
            return;
        }
        double radiusSq = config.getViewerSyncRadius() * config.getViewerSyncRadius();
        LivingEntity entity = minion.getEntity();
        for (Player player : entity.getWorld().getPlayers()) {
            if (!player.isValid() || player.isDead()) {
                continue;
            }
            if (player.getLocation().distanceSquared(entity.getLocation()) <= radiusSq) {
                minion.addViewer(player);
            } else {
                minion.removeViewer(player);
            }
        }
    }

    public void playWalk(SkeMinion minion) {
        if (minion.getModel() == null || config.getWalkAnimation() == null
                || config.getWalkAnimation().equalsIgnoreCase("none")) {
            return;
        }
        try {
            modelEngine.playLoopAnimation(minion.getModel(), config.getWalkAnimation(),
                    config.getBlendIn(), config.getBlendOut());
        } catch (RuntimeException ignored) {
        }
    }

    public void faceTarget(SkeMinion minion, Player target) {
        if (minion.getModel() == null || target == null) {
            return;
        }
        LivingEntity entity = minion.getEntity();
        if (!entity.isValid()) {
            return;
        }
        Location entityLoc = entity.getLocation();
        Location targetLoc = target.getLocation();
        double dx = targetLoc.getX() - entityLoc.getX();
        double dz = targetLoc.getZ() - entityLoc.getZ();
        if (dx * dx + dz * dz < 0.0001) {
            return;
        }
        float yaw = (float) Math.toDegrees(Math.atan2(-dx, dz));
        entity.setRotation(yaw, entityLoc.getPitch());
    }

    public void meleeAttack(SkeMinion minion, Player target) {
        if (!minion.canMelee(800)) {
            return;
        }
        minion.markMelee();
        target.damage(config.getMeleeDamage(), minion.getEntity());
    }

    public void onMinionDeath(SkeMinion minion) {
        String spawnerId = minion.getSpawnerId();
        if (spawnerId == null) {
            return;
        }
        MinionSpawner spawner = spawnerStorage.get(spawnerId);
        if (spawner == null) {
            return;
        }
        spawner.clearActiveMinion();
        spawnerStorage.save();
        scheduleRespawn(spawner);
    }

    private void scheduleRespawn(MinionSpawner spawner) {
        cancelRespawn(spawner.getId());
        long delayTicks = config.getSpawnerRespawnSeconds() * 20L;
        BukkitTask task = Bukkit.getScheduler().runTaskLater(plugin, () -> {
            respawnTasks.remove(spawner.getId());
            try {
                spawnForSpawner(spawner);
            } catch (Exception ex) {
                plugin.getLogger().warning("스포너 리스폰 실패 " + spawner.getId() + ": " + ex.getMessage());
            }
        }, delayTicks);
        respawnTasks.put(spawner.getId(), task);
    }

    private void cancelRespawn(String spawnerId) {
        BukkitTask task = respawnTasks.remove(spawnerId);
        if (task != null) {
            task.cancel();
        }
    }

    public boolean isMinion(LivingEntity entity) {
        return entity != null && entity.hasMetadata(METADATA_KEY);
    }

    public SkeMinion getMinion(UUID id) {
        return minions.get(id);
    }

    public Collection<SkeMinion> getMinions() {
        return minions.values();
    }

    public void remove(SkeMinion minion) {
        LivingEntity entity = minion.getEntity();
        minion.removeAllViewers();
        if (minion.getModel() != null) {
            modelEngine.destroy(minion.getModel());
        }
        minions.remove(minion.getId());
        if (entity.isValid() && !entity.isDead()) {
            entity.remove();
        }
    }

    public void removeAll() {
        for (SkeMinion minion : minions.values().toArray(new SkeMinion[0])) {
            remove(minion);
        }
        for (String id : respawnTasks.keySet().toArray(new String[0])) {
            cancelRespawn(id);
        }
    }

    public MinionSpawner findNearestSpawner(Location location, double maxDistance) {
        MinionSpawner nearest = null;
        double best = maxDistance * maxDistance;
        for (MinionSpawner spawner : spawnerStorage.all()) {
            Location spawnerLoc = spawner.toLocation();
            if (spawnerLoc == null || !spawnerLoc.getWorld().equals(location.getWorld())) {
                continue;
            }
            double dist = spawnerLoc.distanceSquared(location);
            if (dist <= best) {
                best = dist;
                nearest = spawner;
            }
        }
        return nearest;
    }
}
