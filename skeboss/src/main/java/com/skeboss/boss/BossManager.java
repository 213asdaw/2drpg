package com.skeboss.boss;

import com.skeboss.SkeBossPlugin;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.skill.ChainPullSkill;
import com.skeboss.skill.LaserBeamSkill;
import com.skeboss.skill.UntitledSlashSkills;
import com.skeboss.skript.SkriptBridge;
import com.skeboss.util.TextUtil;
import org.bukkit.Bukkit;
import org.bukkit.GameMode;
import org.bukkit.Location;
import org.bukkit.attribute.Attribute;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Mob;
import org.bukkit.entity.Player;
import org.bukkit.entity.Zombie;
import org.bukkit.inventory.ItemStack;
import org.bukkit.inventory.meta.ItemMeta;
import org.bukkit.metadata.FixedMetadataValue;
import org.bukkit.scheduler.BukkitTask;
import org.bukkit.util.Vector;

import java.util.Collection;
import java.util.Comparator;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.logging.Level;

public final class BossManager {

    public static final String METADATA_KEY = "skeboss";

    private final SkeBossPlugin plugin;
    private final ModelEngineBridge modelEngine;
    private final SkriptBridge skriptBridge;
    private BossPresetRegistry presetRegistry;
    private BossConfig defaultConfig;
    private final Map<UUID, SkeBoss> bosses = new ConcurrentHashMap<>();

    public BossManager(SkeBossPlugin plugin, ModelEngineBridge modelEngine, SkriptBridge skriptBridge) {
        this.plugin = plugin;
        this.modelEngine = modelEngine;
        this.skriptBridge = skriptBridge;
        reload();
    }

    public void reload() {
        presetRegistry = new BossPresetRegistry(plugin);
        defaultConfig = presetRegistry.loadDefault(plugin);
    }

    public SkeBoss spawn(Location location) {
        return spawn(presetRegistry.getDefaultPresetId(), location);
    }

    public SkeBoss spawn(String presetId, Location location) {
        BossConfig config = presetRegistry.load(plugin, presetId);
        Location spawnLoc = location.clone();
        spawnLoc.setYaw(spawnLoc.getYaw() + config.getYawOffset());

        Zombie zombie = location.getWorld().spawn(spawnLoc, Zombie.class, entity -> {
            entity.setBaby(false);
            entity.setSilent(true);
            entity.setCanPickupItems(false);
            entity.setRemoveWhenFarAway(false);
            entity.setShouldBurnInDay(false);
            entity.setFireTicks(0);
            entity.setInvisible(config.isHideBaseEntity());
            entity.setCustomNameVisible(true);
            entity.setCustomName(TextUtil.color(config.getDisplayName()));
            entity.setMetadata(METADATA_KEY, new FixedMetadataValue(plugin, true));
            entity.addScoreboardTag("skeboss_preset:" + config.getPresetId());

            Attribute maxHealth = Attribute.GENERIC_MAX_HEALTH;
            if (entity.getAttribute(maxHealth) != null) {
                entity.getAttribute(maxHealth).setBaseValue(config.getMaxHealth());
            }
            entity.setHealth(config.getMaxHealth());

            Attribute follow = Attribute.GENERIC_FOLLOW_RANGE;
            if (entity.getAttribute(follow) != null) {
                entity.getAttribute(follow).setBaseValue(config.getFollowRange());
            }

            Attribute speed = Attribute.GENERIC_MOVEMENT_SPEED;
            if (entity.getAttribute(speed) != null) {
                entity.getAttribute(speed).setBaseValue(config.getMovementSpeed());
            }

            equipHandItem(entity, config);
        });

        SkeBoss boss = new SkeBoss(zombie, null, config);
        bosses.put(zombie.getUniqueId(), boss);

        int delay = Math.max(1, config.getSpawnDelayTicks());
        Bukkit.getScheduler().runTaskLater(plugin, () -> finishSpawn(boss), delay);
        return boss;
    }

    private void equipHandItem(LivingEntity entity, BossConfig config) {
        if (!config.hasHandItem() || entity.getEquipment() == null) {
            return;
        }
        ItemStack hand = new ItemStack(config.getHandMaterial());
        if (config.getHandDisplayName() != null && !config.getHandDisplayName().isBlank()) {
            ItemMeta meta = hand.getItemMeta();
            if (meta != null) {
                meta.setDisplayName(TextUtil.color(config.getHandDisplayName()));
                hand.setItemMeta(meta);
            }
        }
        entity.getEquipment().setItemInMainHand(hand);
        entity.getEquipment().setItemInMainHandDropChance(0.0f);
    }

    private void finishSpawn(SkeBoss boss) {
        LivingEntity entity = boss.getEntity();
        BossConfig config = boss.getConfig();
        if (!entity.isValid() || entity.isDead()) {
            bosses.remove(entity.getUniqueId());
            return;
        }

        try {
            ModelEngineBridge.BossModel bossModel = modelEngine.attachModel(
                    entity,
                    config.getModelId(),
                    config.isHideBaseEntity(),
                    config.getModelScale(),
                    config.getHitboxScale()
            );
            boss.setModel(bossModel);

            playAnimation(boss, config.getWalkAnimation());

            modelEngine.syncNearbyPlayers(bossModel, entity, config.getViewerSyncRadius());
            registerBossBarViewers(boss);

            if (config.isHideBaseEntity()) {
                entity.setInvisible(false);
            }

            plugin.getLogger().info("보스 스폰: " + config.getDisplayName()
                    + " (프리셋: " + config.getPresetId() + ", 모델: " + config.getModelId() + ")");
        } catch (RuntimeException ex) {
            plugin.getLogger().log(Level.SEVERE, "보스 모델 적용 실패 — /meg reload 확인 (모델: "
                    + config.getModelId() + ")", ex);
            entity.setInvisible(false);
            if (!config.isHideBaseEntity()) {
                entity.setCustomName(TextUtil.color(config.getDisplayName() + " &7(모델 로드 실패)"));
            }
        }
    }

    private void registerBossBarViewers(SkeBoss boss) {
        BossConfig config = boss.getConfig();
        for (Player online : Bukkit.getOnlinePlayers()) {
            if (online.getWorld().equals(boss.getEntity().getWorld())
                    && online.getLocation().distanceSquared(boss.getEntity().getLocation())
                    <= config.getViewerSyncRadius() * config.getViewerSyncRadius()) {
                boss.addViewer(online);
            }
        }
    }

    public void syncBossViewers(SkeBoss boss, Player player) {
        BossConfig config = boss.getConfig();
        if (boss.getModel() != null) {
            modelEngine.syncNearbyPlayers(boss.getModel(), boss.getEntity(), config.getViewerSyncRadius());
        }
        boss.addViewer(player);
    }

    public void playIdle(SkeBoss boss) {
        playAnimation(boss, boss.getConfig().getIdleAnimation());
    }

    public void playWalk(SkeBoss boss) {
        playAnimation(boss, boss.getConfig().getWalkAnimation());
    }

    public void playAnimation(SkeBoss boss, String animation) {
        BossConfig config = boss.getConfig();
        if (!boss.isReady() || animation == null || animation.isBlank() || "none".equalsIgnoreCase(animation)) {
            return;
        }
        if (animation.equals(boss.getCurrentAnimation())) {
            return;
        }
        modelEngine.playLoopAnimation(
                boss.getModel(),
                animation,
                config.getBlendIn(),
                config.getBlendOut()
        );
        boss.setCurrentAnimation(animation);
    }

    public void clearAnimationState(SkeBoss boss) {
        boss.setCurrentAnimation(null);
    }

    public void faceTarget(SkeBoss boss, Player target) {
        faceTarget(boss, target, boss.isCastingSkill());
    }

    public void faceTarget(SkeBoss boss, Player target, boolean forSkill) {
        if (target == null || !boss.getEntity().isValid()) {
            return;
        }
        BossConfig config = boss.getConfig();
        LivingEntity entity = boss.getEntity();
        Location aim = getTargetAimPoint(target);
        Location origin = forSkill ? getBeamOrigin(boss) : entity.getLocation();

        double dx = aim.getX() - origin.getX();
        double dy = aim.getY() - origin.getY();
        double dz = aim.getZ() - origin.getZ();
        double horizontal = Math.sqrt(dx * dx + dz * dz);
        if (horizontal < 0.0001 && Math.abs(dy) < 0.0001) {
            return;
        }

        float offset = forSkill ? config.getSkillFaceYawOffset() : config.getFaceYawOffset();
        float yaw = (float) Math.toDegrees(Math.atan2(-dx, dz)) + offset;
        float pitch = 0.0f;
        if (forSkill && horizontal > 0.0001) {
            pitch = (float) -Math.toDegrees(Math.atan2(dy, horizontal));
            pitch = clampPitch(pitch, config);
        }
        entity.setRotation(yaw, pitch);
        if (boss.getModel() != null) {
            modelEngine.syncBodyRotation(boss.getModel(), yaw);
        }
    }

    private float clampPitch(float pitch, BossConfig config) {
        float max = config.getSkillMaxPitch();
        return Math.max(-max, Math.min(max, pitch));
    }

    public Location getTargetAimPoint(Player target) {
        return target.getLocation().clone().add(0, target.getHeight() * 0.5, 0);
    }

    public boolean isValidTarget(Player player) {
        return player != null
                && player.isValid()
                && !player.isDead()
                && player.getGameMode() != GameMode.SPECTATOR
                && player.getGameMode() != GameMode.CREATIVE;
    }

    public boolean isEnemy(SkeBoss boss, Player player) {
        if (!isValidTarget(player)) {
            return false;
        }
        BossConfig config = boss.getConfig();
        if ("aggro".equalsIgnoreCase(config.getTargetMode())) {
            return boss.hasAggro(player, config.getAggroDropMs());
        }
        return true;
    }

    public Player findNearestEnemy(SkeBoss boss, LivingEntity entity, double range) {
        return entity.getWorld().getPlayers().stream()
                .filter(player -> isEnemy(boss, player))
                .filter(player -> player.getLocation().distanceSquared(entity.getLocation()) <= range * range)
                .min(Comparator.comparingDouble(player -> player.getLocation().distanceSquared(entity.getLocation())))
                .orElse(null);
    }

    public Player findNearestPlayer(LivingEntity entity, double range) {
        return entity.getWorld().getPlayers().stream()
                .filter(this::isValidTarget)
                .filter(player -> player.getLocation().distanceSquared(entity.getLocation()) <= range * range)
                .min(Comparator.comparingDouble(player -> player.getLocation().distanceSquared(entity.getLocation())))
                .orElse(null);
    }

    public boolean isSkillTarget(SkeBoss boss, Player player, double range) {
        if (!isValidTarget(player)) {
            return false;
        }
        if (!player.getWorld().equals(boss.getEntity().getWorld())) {
            return false;
        }
        if (player.getLocation().distanceSquared(boss.getEntity().getLocation()) > range * range) {
            return false;
        }
        if ("nearest".equalsIgnoreCase(boss.getConfig().getSkillTargetMode())) {
            return true;
        }
        return isEnemy(boss, player);
    }

    public Player resolveSkillTarget(SkeBoss boss, double range) {
        Player current = boss.getTarget();
        if (current != null && isSkillTarget(boss, current, range)) {
            return current;
        }
        if ("nearest".equalsIgnoreCase(boss.getConfig().getSkillTargetMode())) {
            return findNearestPlayer(boss.getEntity(), range);
        }
        return findNearestEnemy(boss, boss.getEntity(), range);
    }

    public void startSkillTracking(SkeBoss boss, double trackRange) {
        stopSkillTracking(boss);
        BukkitTask task = Bukkit.getScheduler().runTaskTimer(plugin, () -> {
            if (!boss.getEntity().isValid() || boss.getEntity().isDead() || !boss.isCastingSkill()) {
                stopSkillTracking(boss);
                return;
            }
            Player target = resolveSkillTarget(boss, trackRange);
            if (target != null) {
                boss.setTarget(target);
                faceTarget(boss, target, true);
            }
        }, 0L, 1L);
        boss.setAimTask(task);
    }

    public void stopSkillTracking(SkeBoss boss) {
        boss.cancelAimTask();
    }

    public Location getBeamOrigin(SkeBoss boss) {
        return getSkillOrigin(boss, 0.75);
    }

    public Location getSkillOrigin(SkeBoss boss) {
        return getSkillOrigin(boss, 0.55);
    }

    private Location getSkillOrigin(SkeBoss boss, double heightRatio) {
        LivingEntity entity = boss.getEntity();
        Location loc = entity.getLocation().clone();
        loc.add(0, entity.getHeight() * heightRatio, 0);
        loc.setPitch(0.0f);
        return loc;
    }

    public Vector getBeamDirection(SkeBoss boss, double range) {
        Player target = resolveSkillTarget(boss, range);
        Location origin = getBeamOrigin(boss);

        if (target != null) {
            boss.setTarget(target);
            Vector toTarget = getTargetAimPoint(target).toVector().subtract(origin.toVector());
            if (toTarget.lengthSquared() > 0.0001) {
                return toTarget.normalize();
            }
        }

        return directionFromRotation(boss.getEntity().getLocation().getYaw(), boss.getEntity().getLocation().getPitch());
    }

    private Vector directionFromRotation(float yaw, float pitch) {
        double yawRad = Math.toRadians(yaw);
        double pitchRad = Math.toRadians(pitch);
        return new Vector(
                -Math.sin(yawRad) * Math.cos(pitchRad),
                -Math.sin(pitchRad),
                Math.cos(yawRad) * Math.cos(pitchRad)
        ).normalize();
    }

    public double getBeamDamage(SkeBoss boss) {
        BossConfig config = boss.getConfig();
        LivingEntity entity = boss.getEntity();
        double attackStat = skriptBridge.getEntityStat(
                entity,
                config.getSkriptAttackVariable(),
                config.getFallbackAttackStat()
        );
        return Math.max(0.0, attackStat * config.getBeamAttackMultiplier());
    }

    public boolean castSkill(SkeBoss boss, SkillDefinition skill) {
        if (boss.isCastingSkill() || !boss.isReady() || !boss.isSkillReady(skill)) {
            return false;
        }

        if (skill.isUntitledSkill()) {
            return castUntitledSkill(boss, skill);
        }

        LivingEntity entity = boss.getEntity();
        BossConfig config = boss.getConfig();

        double trackRange = Math.max(skill.range(), config.getFollowRange());
        Player target = resolveSkillTarget(boss, trackRange);
        if (target == null) {
            return false;
        }

        boss.setCastingSkill(true);
        boss.setCurrentSkillId(skill.id());
        boss.setSkillCooldown(skill);

        if (entity instanceof Mob mob) {
            mob.setAI(false);
        }
        entity.setVelocity(new Vector(0, 0, 0));

        boss.setTarget(target);
        faceTarget(boss, target, true);

        startSkillTracking(boss, trackRange);

        modelEngine.playLoopAnimation(
                boss.getModel(),
                skill.animation(),
                config.getBlendIn(),
                config.getBlendOut()
        );
        boss.setCurrentAnimation(skill.animation());

        int duration = modelEngine.estimateDurationTicks(boss.getModel(), skill.animation(), skill.durationTicks());

        if (skill.isBeamSkill()) {
            LaserBeamSkill.execute(plugin, this, boss, skill);
        } else if (skill.isChainSkill()) {
            ChainPullSkill.execute(plugin, this, boss, skill);
        } else {
            Bukkit.getScheduler().runTaskLater(plugin, () -> applySkillDamage(boss, skill), Math.max(5, duration / 2));
        }
        Bukkit.getScheduler().runTaskLater(plugin, () -> finishSkill(boss, skill), duration);
        return true;
    }

    private boolean castUntitledSkill(SkeBoss boss, SkillDefinition skill) {
        LivingEntity entity = boss.getEntity();
        BossConfig config = boss.getConfig();

        double trackRange = Math.max(skill.range(), config.getFollowRange());
        Player target = resolveSkillTarget(boss, trackRange);
        String untitledId = skill.untitledSkill().toLowerCase();
        if (target == null && (untitledId.contains("slash") || untitledId.contains("검기"))) {
            return false;
        }

        boss.setCastingSkill(true);
        boss.setCurrentSkillId(skill.id());
        boss.setSkillCooldown(skill);

        if (entity instanceof Mob mob) {
            mob.setAI(false);
        }
        entity.setVelocity(new Vector(0, 0, 0));

        if (target != null) {
            boss.setTarget(target);
            faceTarget(boss, target, true);
            startSkillTracking(boss, trackRange);
        }

        boolean cast = UntitledSlashSkills.cast(
                plugin,
                boss,
                skill.untitledSkill(),
                config.getSkriptAttackVariable(),
                config.getFallbackAttackStat()
        );
        if (!cast) {
            boss.setCastingSkill(false);
            boss.setCurrentSkillId(null);
            if (entity instanceof Mob mob) {
                mob.setAI(true);
            }
            return false;
        }

        int duration = skill.durationTicks();
        Bukkit.getScheduler().runTaskLater(plugin, () -> finishSkill(boss, skill), duration);
        return true;
    }

    private void applySkillDamage(SkeBoss boss, SkillDefinition skill) {
        LivingEntity entity = boss.getEntity();
        if (!entity.isValid()) {
            return;
        }

        Location origin = entity.getLocation();
        Collection<EntityTarget> targets = findTargets(boss, origin, skill.range(), skill.aoeRadius());
        for (EntityTarget target : targets) {
            target.player().damage(skill.damage(), entity);
            Vector knockback = target.player().getLocation().toVector()
                    .subtract(origin.toVector())
                    .normalize()
                    .multiply(skill.knockback());
            knockback.setY(0.35);
            target.player().setVelocity(knockback);
        }
    }

    public void meleeAttack(SkeBoss boss, Player target) {
        if (boss.isCastingSkill() || !isEnemy(boss, target)) {
            return;
        }
        target.damage(boss.getConfig().getMeleeDamage(), boss.getEntity());
    }

    private void finishSkill(SkeBoss boss, SkillDefinition skill) {
        LivingEntity entity = boss.getEntity();

        if (!entity.isValid()) {
            return;
        }

        stopSkillTracking(boss);

        if (!skill.isUntitledSkill() && skill.animation() != null && !skill.animation().isBlank()) {
            modelEngine.stopAnimation(boss.getModel(), skill.animation());
        }
        clearAnimationState(boss);
        playWalk(boss);

        if (entity instanceof Mob mob) {
            mob.setAI(true);
        }

        boss.setCastingSkill(false);
        boss.setCurrentSkillId(null);
    }

    private Collection<EntityTarget> findTargets(SkeBoss boss, Location origin, double range, double aoeRadius) {
        Map<UUID, EntityTarget> found = new ConcurrentHashMap<>();
        double checkRadius = aoeRadius > 0 ? aoeRadius : range;

        for (Player player : origin.getWorld().getPlayers()) {
            if (!isEnemy(boss, player)) {
                continue;
            }
            double distance = player.getLocation().distance(origin);
            if (distance > checkRadius) {
                continue;
            }
            if (aoeRadius <= 0 && distance > range) {
                continue;
            }
            found.put(player.getUniqueId(), new EntityTarget(player, distance));
        }
        return found.values();
    }

    public void remove(SkeBoss boss) {
        bosses.remove(boss.getId());
        boss.removeAllViewers();
        if (boss.getModel() != null) {
            modelEngine.destroy(boss.getModel());
        }
        if (boss.getEntity().isValid()) {
            boss.getEntity().remove();
        }
    }

    public void removeAll() {
        bosses.values().forEach(this::remove);
        bosses.clear();
    }

    public SkeBoss getBoss(UUID id) {
        return bosses.get(id);
    }

    public Collection<SkeBoss> getBosses() {
        return bosses.values();
    }

    public boolean isBoss(LivingEntity entity) {
        return bosses.containsKey(entity.getUniqueId());
    }

    public BossConfig getConfig() {
        return defaultConfig;
    }

    public BossPresetRegistry getPresetRegistry() {
        return presetRegistry;
    }

    public SkriptBridge getSkriptBridge() {
        return skriptBridge;
    }

    private record EntityTarget(Player player, double distance) {
    }
}
