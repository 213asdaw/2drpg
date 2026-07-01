package com.skeboss.minion;

import com.skeboss.SkeBossPlugin;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.util.TextUtil;
import org.bukkit.Bukkit;
import org.bukkit.Location;
import org.bukkit.attribute.Attribute;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Mob;
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
        spawnerStorage.clearAllActiveMinions();
        for (MinionSpawner spawner : spawnerStorage.all()) {
            Location location = spawner.toLocation();
            if (location == null) {
                plugin.getLogger().warning("스포너 월드 없음: " + spawner.getId());
                continue;
            }
            ensureSpawnerMinion(spawner);
        }
    }

    public void ensureSpawnerMinion(MinionSpawner spawner) {
        if (spawner.getActiveMinionId() != null) {
            SkeMinion existing = minions.get(spawner.getActiveMinionId());
            if (existing != null && existing.getEntity().isValid() && !existing.getEntity().isDead()) {
                registerBossBarViewers(existing);
                return;
            }
        }
        try {
            spawnForSpawner(spawner);
        } catch (Exception ex) {
            plugin.getLogger().log(Level.WARNING, "스포너 잡몹 스폰 실패 " + spawner.getId(), ex);
        }
    }

    public void shutdownPersist() {
        for (MinionSpawner spawner : spawnerStorage.all()) {
            spawner.clearActiveMinion();
        }
        spawnerStorage.save();
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
            entity.setInvisible(true);
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
            entity.setAware(!config.isBacklineMode());
            if (entity.getAttribute(follow) != null) {
                entity.getAttribute(follow).setBaseValue(config.getFollowRange());
            }
        });

        SkeMinion minion = new SkeMinion(zombie, spawnerId, config);
        minion.setHomeLocation(spawnLoc);
        minions.put(zombie.getUniqueId(), minion);

        int delay = Math.max(0, config.getSpawnDelayTicks());
        if (delay == 0) {
            finishSpawn(minion);
        } else {
            Bukkit.getScheduler().runTaskLater(plugin, () -> finishSpawn(minion), delay);
        }
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
                plugin.getLogger().info("잡몹 PlayerLimb — 스킨 적용 단계에서 본 생성 예정 (" + resolvedModelId + ")");
            }

            minion.setModel(model);
            double syncRadius = config.getViewerSyncRadius();
            modelEngine.applyPlayerSkin(model, entity, config.getSkinUsername(), syncRadius, appliedLimbs -> {
                int requiredLimbs = Math.max(1, modelEngine.countPlayerLimbs(model));
                if (appliedLimbs >= requiredLimbs && config.isHideBaseEntity()) {
                    if (!entity.isValid() || entity.isDead()) {
                        return;
                    }
                    modelEngine.setBaseEntityVisible(model, entity, false, syncRadius);
                    modelEngine.forceResyncNearbyPlayers(model, entity, syncRadius);
                    entity.setInvisible(false);
                } else if (appliedLimbs > 0 && appliedLimbs < requiredLimbs) {
                    modelEngine.restoreBaseEntityVisibility(entity);
                    entity.setInvisible(false);
                    plugin.getLogger().warning("잡몹 스킨 일부만 적용 (" + appliedLimbs
                            + "개) — 좀비 본체 유지");
                } else if (appliedLimbs == 0) {
                    modelEngine.restoreBaseEntityVisibility(entity);
                    entity.setInvisible(false);
                    plugin.getLogger().warning("잡몹 스킨 미적용 — 좀비 본체를 유지합니다.");
                } else {
                    entity.setInvisible(false);
                }
            });
            registerBossBarViewers(minion);
            minion.setReady(true);
            plugin.getLogger().info("잡몹 스폰: " + config.getSkinUsername() + " (모델: " + resolvedModelId
                    + ") @ " + entity.getLocation());
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
        if (minion.isAttacking()) {
            return;
        }
        try {
            modelEngine.playLoopAnimation(minion.getModel(), config.getWalkAnimation(),
                    config.getBlendIn(), config.getBlendOut());
        } catch (RuntimeException ignored) {
        }
    }

    public void playAttack(SkeMinion minion) {
        String attack = config.getAttackAnimation();
        if (minion.getModel() == null || attack == null || attack.equalsIgnoreCase("none")) {
            return;
        }
        try {
            minion.setAttacking(true);
            String walk = config.getWalkAnimation();
            if (walk != null && !walk.equalsIgnoreCase("none")) {
                modelEngine.stopAnimation(minion.getModel(), walk);
            }
            modelEngine.playAnimation(minion.getModel(), attack,
                    config.getBlendIn(), config.getBlendOut(), false);
            int ticks = modelEngine.estimateDurationTicks(minion.getModel(), attack, 10);
            Bukkit.getScheduler().runTaskLater(plugin, () -> {
                minion.setAttacking(false);
                if (minion.getEntity().isValid() && !minion.getEntity().isDead()) {
                    playWalk(minion);
                }
            }, ticks);
        } catch (RuntimeException ex) {
            minion.setAttacking(false);
            plugin.getLogger().fine("잡몹 공격 애니 실패: " + attack);
        }
    }

    public void faceTarget(SkeMinion minion, Player target) {
        if (target == null) {
            return;
        }
        faceLocation(minion, target.getLocation());
    }

    public void faceLocation(SkeMinion minion, Location targetLoc) {
        if (minion.getModel() == null || targetLoc == null) {
            return;
        }
        LivingEntity entity = minion.getEntity();
        if (!entity.isValid()) {
            return;
        }
        Location entityLoc = entity.getLocation();
        double dx = targetLoc.getX() - entityLoc.getX();
        double dz = targetLoc.getZ() - entityLoc.getZ();
        if (dx * dx + dz * dz < 0.0001) {
            return;
        }
        float yaw = (float) Math.toDegrees(Math.atan2(-dx, dz));
        entity.setRotation(yaw, entityLoc.getPitch());
    }

    public void returnToHome(SkeMinion minion) {
        Location home = minion.getHomeLocation();
        if (home == null || home.getWorld() == null) {
            return;
        }
        LivingEntity entity = minion.getEntity();
        if (!entity.isValid() || entity.isDead()) {
            return;
        }
        if (!entity.getWorld().equals(home.getWorld())) {
            return;
        }
        double dist = entity.getLocation().distance(home);
        if (dist <= config.getHomeTolerance()) {
            return;
        }
        if (entity instanceof Mob mob) {
            mob.getPathfinder().moveTo(home, config.getMovementSpeed());
        }
        faceLocation(minion, home);
        playWalk(minion);
    }

    public void meleeAttack(SkeMinion minion, Player target) {
        if (!minion.canMelee(800)) {
            return;
        }
        minion.markMelee();
        playAttack(minion);
        target.damage(config.getMeleeDamage(), minion.getEntity());
    }

    public void onMinionDeath(SkeMinion minion) {
        String spawnerId = minion.getSpawnerId();
        if (spawnerId == null) {
            plugin.getLogger().fine("잡몹 사망 (스포너 없음) — 리스폰 안 함");
            return;
        }
        MinionSpawner spawner = spawnerStorage.get(spawnerId);
        if (spawner == null) {
            plugin.getLogger().warning("잡몹 사망 — 스포너 없음: " + spawnerId);
            return;
        }
        spawner.clearActiveMinion();
        spawnerStorage.save();
        plugin.getLogger().info("잡몹 사망 — 스포너 " + spawnerId + " / "
                + config.getSpawnerRespawnSeconds() + "초 후 리스폰");
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
        if (entity == null) {
            return false;
        }
        if (entity.hasMetadata(METADATA_KEY)) {
            return true;
        }
        return entity.getScoreboardTags().contains(SCOREBOARD_TAG);
    }

    public SkeMinion resolveMinion(LivingEntity entity) {
        if (entity == null) {
            return null;
        }
        SkeMinion minion = minions.get(entity.getUniqueId());
        if (minion != null) {
            return minion;
        }
        return isMinion(entity) ? minions.get(entity.getUniqueId()) : null;
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
