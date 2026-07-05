package com.skeboss.cutscene;

import com.skeboss.SkeBossPlugin;
import com.skeboss.minion.MinionManager;
import com.skeboss.minion.MinionPreset;
import com.skeboss.minion.SkinTexturesUtil;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.util.TextUtil;
import org.bukkit.Bukkit;
import org.bukkit.GameMode;
import org.bukkit.Location;
import org.bukkit.Particle;
import org.bukkit.attribute.Attribute;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.entity.Zombie;
import org.bukkit.metadata.FixedMetadataValue;
import org.bukkit.scheduler.BukkitTask;
import org.bukkit.util.Vector;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.IntConsumer;
import java.util.logging.Level;

public final class CutsceneManager {

    public static final String ACTOR_TAG = "skeboss_cutscene_actor";
    public static final String METADATA_KEY = "skeboss_cutscene";

    private final SkeBossPlugin plugin;
    private final ModelEngineBridge modelEngine;
    private final MinionManager minionManager;
    private final CutsceneLoader loader;

    private final Map<String, CutsceneDefinition> scenes = new LinkedHashMap<>();
    private final Map<UUID, CutsceneSession> sessions = new ConcurrentHashMap<>();

    public CutsceneManager(SkeBossPlugin plugin, ModelEngineBridge modelEngine, MinionManager minionManager) {
        this.plugin = plugin;
        this.modelEngine = modelEngine;
        this.minionManager = minionManager;
        this.loader = new CutsceneLoader(plugin);
        reload();
    }

    public void reload() {
        scenes.clear();
        scenes.putAll(loader.loadAll());
        plugin.getLogger().info("컷신 " + scenes.size() + "개 로드");
    }

    public Map<String, CutsceneDefinition> getScenes() {
        return Map.copyOf(scenes);
    }

    public boolean isPlaying(Player player) {
        return sessions.containsKey(player.getUniqueId());
    }

    public boolean play(Player viewer, String sceneId) {
        CutsceneDefinition definition = scenes.get(sceneId);
        if (definition == null) {
            return false;
        }
        if (isPlaying(viewer)) {
            stop(viewer, false);
        }
        CutsceneSession session = new CutsceneSession(viewer, definition);
        sessions.put(viewer.getUniqueId(), session);
        session.begin();
        return true;
    }

    public void stop(Player player, boolean completed) {
        CutsceneSession session = sessions.remove(player.getUniqueId());
        if (session != null) {
            session.end(completed);
        }
    }

    CutsceneSession getSession(Player player) {
        return sessions.get(player.getUniqueId());
    }

    boolean shouldBypassMoveLock(Player player) {
        CutsceneSession session = getSession(player);
        return session != null && session.consumeTeleportBypass();
    }

    private final class CutsceneSession {

        private final Player viewer;
        private final CutsceneDefinition definition;
        private final Location anchor;
        private final Map<String, CutsceneActor> actors = new LinkedHashMap<>();

        private int stepIndex;
        private BukkitTask stepTask;
        private BukkitTask cameraTask;
        private boolean teleportBypass;

        private Location savedLocation;
        private GameMode savedGameMode;
        private boolean savedAllowFlight;
        private boolean savedFlying;
        private float savedWalkSpeed;
        private float savedFlySpeed;

        private double[] currentCameraOffset = {0, 2.5, 7};
        private final java.util.Set<String> burningActors = new java.util.LinkedHashSet<>();
        private BukkitTask fireParticleTask;

        private CutsceneSession(Player viewer, CutsceneDefinition definition) {
            this.viewer = viewer;
            this.definition = definition;
            this.anchor = viewer.getLocation().clone();
        }

        private void begin() {
            saveViewerState();
            viewer.sendMessage(TextUtil.color("&7[연출] &f" + definition.getId() + " 시작"));
            runNextStep();
        }

        private void end(boolean completed) {
            cancelTasks();
            cleanupActors();
            restoreViewerState();
            CutsceneEndEvent.fire(plugin, viewer, definition.getId(), completed);
            viewer.sendMessage(TextUtil.color(completed
                    ? "&7[연출] &a" + definition.getId() + " 완료"
                    : "&7[연출] &c" + definition.getId() + " 중단"));
        }

        private void saveViewerState() {
            savedLocation = viewer.getLocation().clone();
            savedGameMode = viewer.getGameMode();
            savedAllowFlight = viewer.getAllowFlight();
            savedFlying = viewer.isFlying();
            savedWalkSpeed = viewer.getWalkSpeed();
            savedFlySpeed = viewer.getFlySpeed();
        }

        private void restoreViewerState() {
            if (!viewer.isOnline()) {
                return;
            }
            viewer.setWalkSpeed(savedWalkSpeed);
            viewer.setFlySpeed(savedFlySpeed);
            viewer.setAllowFlight(savedAllowFlight);
            viewer.setFlying(savedFlying);
            viewer.setGameMode(savedGameMode);
            if (savedLocation != null) {
                teleportViewer(savedLocation);
            }
        }

        private void runNextStep() {
            List<CutsceneStep> steps = definition.getSteps();
            if (stepIndex >= steps.size()) {
                CutsceneManager.this.stop(viewer, true);
                return;
            }
            CutsceneStep step = steps.get(stepIndex++);
            executeStep(step, this::runNextStep);
        }

        private void executeStep(CutsceneStep step, Runnable onComplete) {
            if (step instanceof CutsceneStep.Freeze freeze) {
                applyFreeze(freeze.enabled());
                onComplete.run();
            } else if (step instanceof CutsceneStep.Restore) {
                restoreViewerState();
                onComplete.run();
            } else if (step instanceof CutsceneStep.Cleanup) {
                cleanupActors();
                onComplete.run();
            } else if (step instanceof CutsceneStep.Wait wait) {
                schedule(onComplete, wait.ticks());
            } else if (step instanceof CutsceneStep.Title title) {
                viewer.sendTitle(
                        TextUtil.color(title.main()),
                        TextUtil.color(title.sub()),
                        title.fadeIn(),
                        title.stay(),
                        title.fadeOut()
                );
                onComplete.run();
            } else if (step instanceof CutsceneStep.Message message) {
                viewer.sendMessage(TextUtil.color(message.text()));
                onComplete.run();
            } else if (step instanceof CutsceneStep.Sfx sfx) {
                viewer.playSound(viewer.getLocation(), sfx.sound(), sfx.volume(), sfx.pitch());
                onComplete.run();
            } else if (step instanceof CutsceneStep.Camera camera) {
                applyCamera(camera);
                onComplete.run();
            } else if (step instanceof CutsceneStep.Spawn spawn) {
                spawnActor(spawn, ignored -> onComplete.run());
            } else if (step instanceof CutsceneStep.Move move) {
                moveActor(move, onComplete);
            } else if (step instanceof CutsceneStep.Animate animate) {
                playActorAnimation(animate);
                schedule(onComplete, Math.max(1, animate.durationTicks()));
            } else if (step instanceof CutsceneStep.StopAnimation stop) {
                stopActorAnimation(stop);
                onComplete.run();
            } else if (step instanceof CutsceneStep.Despawn despawn) {
                despawnActor(despawn.actorId());
                onComplete.run();
            } else if (step instanceof CutsceneStep.Fire fire) {
                applyFire(fire);
                onComplete.run();
            } else if (step instanceof CutsceneStep.Particle particle) {
                spawnParticles(particle);
                onComplete.run();
            } else if (step instanceof CutsceneStep.CameraMove cameraMove) {
                applyCameraMove(cameraMove, onComplete);
            } else if (step instanceof CutsceneStep.AnimateMulti multi) {
                playAnimateMulti(multi);
                int maxTicks = multi.entries().stream()
                        .mapToInt(CutsceneStep.AnimateMultiEntry::durationTicks)
                        .max().orElse(20);
                schedule(onComplete, Math.max(1, maxTicks));
            } else if (step instanceof CutsceneStep.Clash clash) {
                playClash(clash);
                schedule(onComplete, Math.max(1, clash.durationTicks()));
            } else {
                onComplete.run();
            }
        }

        private void teleportViewer(Location location) {
            teleportBypass = true;
            viewer.teleport(location);
        }

        private boolean consumeTeleportBypass() {
            if (!teleportBypass) {
                return false;
            }
            teleportBypass = false;
            return true;
        }
        private void applyFreeze(boolean enabled) {
            if (enabled) {
                viewer.setWalkSpeed(0f);
                viewer.setFlySpeed(0f);
                viewer.setAllowFlight(true);
                viewer.setFlying(true);
                if (viewer.getGameMode() == GameMode.SURVIVAL || viewer.getGameMode() == GameMode.ADVENTURE) {
                    viewer.setGameMode(GameMode.ADVENTURE);
                }
            } else {
                viewer.setWalkSpeed(savedWalkSpeed);
                viewer.setFlySpeed(savedFlySpeed);
            }
        }

        private void applyCamera(CutsceneStep.Camera camera) {
            if (cameraTask != null) {
                cameraTask.cancel();
                cameraTask = null;
            }
            currentCameraOffset = camera.offset().clone();
            Location cameraLoc = offsetLocation(currentCameraOffset);
            applyLookAt(cameraLoc, camera.lookAtActor(), List.of());
            if (camera.yaw() != null && camera.pitch() != null) {
                cameraLoc.setYaw(camera.yaw());
                cameraLoc.setPitch(camera.pitch());
            }
            teleportViewer(cameraLoc);

            if (camera.hold()) {
                startCameraHold(currentCameraOffset, camera.lookAtActor(), List.of());
            }
        }

        private void applyCameraMove(CutsceneStep.CameraMove move, Runnable onComplete) {
            if (cameraTask != null) {
                cameraTask.cancel();
                cameraTask = null;
            }
            double[] from = currentCameraOffset.clone();
            double[] to = move.toOffset();
            int duration = Math.max(1, move.durationTicks());
            final int[] tick = {0};
            stepTask = new org.bukkit.scheduler.BukkitRunnable() {
                @Override
                public void run() {
                    tick[0]++;
                    double t = easeInOut(Math.min(1.0, tick[0] / (double) duration));
                    currentCameraOffset = lerpOffset(from, to, t);
                    Location cameraLoc = offsetLocation(currentCameraOffset);
                    applyLookAt(cameraLoc, move.lookAtActor(), move.lookAtMidpoint());
                    teleportViewer(cameraLoc);
                    if (tick[0] >= duration) {
                        cancel();
                        if (move.hold()) {
                            startCameraHold(currentCameraOffset, move.lookAtActor(), move.lookAtMidpoint());
                        }
                        onComplete.run();
                    }
                }
            }.runTaskTimer(plugin, 0L, 1L);
        }

        private void startCameraHold(double[] offset, String lookAtActor, List<String> lookAtMidpoint) {
            cameraTask = Bukkit.getScheduler().runTaskTimer(plugin, () -> {
                if (!viewer.isOnline()) {
                    return;
                }
                Location next = offsetLocation(offset);
                applyLookAt(next, lookAtActor, lookAtMidpoint);
                teleportViewer(next);
            }, 1L, 1L);
        }

        private void applyLookAt(Location cameraLoc, String lookAtActor, List<String> lookAtMidpoint) {
            if (lookAtMidpoint != null && lookAtMidpoint.size() >= 2) {
                CutsceneActor first = actors.get(lookAtMidpoint.get(0));
                CutsceneActor second = actors.get(lookAtMidpoint.get(1));
                if (first != null && second != null && first.isValid() && second.isValid()) {
                    Vector mid = first.getLocation().toVector().add(second.getLocation().toVector())
                            .multiply(0.5).add(new Vector(0, 1.2, 0));
                    faceLocation(cameraLoc, mid.toLocation(cameraLoc.getWorld()));
                    return;
                }
            }
            if (lookAtActor != null && !lookAtActor.isBlank()) {
                CutsceneActor actor = actors.get(lookAtActor);
                if (actor != null && actor.isValid()) {
                    faceLocation(cameraLoc, actor.getLocation().clone().add(0, 1.2, 0));
                }
            }
        }

        private void applyFire(CutsceneStep.Fire fire) {
            if (!fire.enable()) {
                burningActors.clear();
                stopFireParticles();
                for (CutsceneActor actor : actors.values()) {
                    actor.getEntity().setFireTicks(0);
                }
                return;
            }
            for (String actorId : fire.actors()) {
                if ("all".equalsIgnoreCase(actorId)) {
                    burningActors.addAll(actors.keySet());
                } else {
                    burningActors.add(actorId);
                }
            }
            for (String actorId : burningActors) {
                CutsceneActor actor = actors.get(actorId);
                if (actor != null && actor.isValid()) {
                    actor.getEntity().setFireTicks(Integer.MAX_VALUE);
                }
            }
            ensureFireParticles();
        }

        private void ensureFireParticles() {
            if (fireParticleTask != null || burningActors.isEmpty()) {
                return;
            }
            fireParticleTask = Bukkit.getScheduler().runTaskTimer(plugin, () -> {
                if (burningActors.isEmpty()) {
                    stopFireParticles();
                    return;
                }
                for (String actorId : burningActors) {
                    CutsceneActor actor = actors.get(actorId);
                    if (actor == null || !actor.isValid()) {
                        continue;
                    }
                    Location flame = actor.getLocation().clone().add(0, 1.0, 0);
                    flame.getWorld().spawnParticle(Particle.FLAME, flame, 10, 0.28, 0.55, 0.28, 0.02);
                    flame.getWorld().spawnParticle(Particle.CAMPFIRE_COSY_SMOKE, flame, 4, 0.2, 0.35, 0.2, 0.01);
                    flame.getWorld().spawnParticle(Particle.LAVA, flame, 1, 0.15, 0.2, 0.15, 0);
                }
            }, 0L, 3L);
        }

        private void stopFireParticles() {
            if (fireParticleTask != null) {
                fireParticleTask.cancel();
                fireParticleTask = null;
            }
        }

        private void spawnParticles(CutsceneStep.Particle particle) {
            Location base;
            if (particle.actor() != null && !particle.actor().isBlank()) {
                CutsceneActor actor = actors.get(particle.actor());
                if (actor == null) {
                    return;
                }
                base = actor.getLocation().clone().add(0, 1.0, 0);
            } else {
                base = offsetLocation(particle.offset()).add(0, 1.0, 0);
            }
            Particle type = parseParticleType(particle.particle());
            double spread = particle.spread();
            base.getWorld().spawnParticle(type, base, particle.count(), spread, spread, spread, 0.02);
        }

        private void playAnimateMulti(CutsceneStep.AnimateMulti multi) {
            for (CutsceneStep.AnimateMultiEntry entry : multi.entries()) {
                playActorAnimation(new CutsceneStep.Animate(
                        entry.actorId(), entry.animation(), entry.durationTicks(), entry.loop()));
            }
        }

        private void playClash(CutsceneStep.Clash clash) {
            CutsceneActor actorA = actors.get(clash.actorA());
            CutsceneActor actorB = actors.get(clash.actorB());
            if (actorA != null && actorA.getModel() != null) {
                modelEngine.playAnimation(actorA.getModel(), clash.animation(), 0.05, 0.1, false);
            }
            if (actorB != null && actorB.getModel() != null) {
                modelEngine.playAnimation(actorB.getModel(), clash.animation(), 0.05, 0.1, false);
            }
            if (actorA != null && actorB != null) {
                Vector mid = actorA.getLocation().toVector().add(actorB.getLocation().toVector())
                        .multiply(0.5).add(new Vector(0, 1.3, 0));
                Location hit = mid.toLocation(actorA.getEntity().getWorld());
                hit.getWorld().spawnParticle(Particle.CRIT, hit, 20, 0.15, 0.2, 0.15, 0.15);
                hit.getWorld().spawnParticle(Particle.FIREWORKS_SPARK, hit, 15, 0.1, 0.15, 0.1, 0.08);
                hit.getWorld().spawnParticle(Particle.SWEEP_ATTACK, hit, 2, 0, 0, 0, 0);
                viewer.playSound(hit, org.bukkit.Sound.ENTITY_ZOMBIE_ATTACK_IRON_DOOR, 1.0f, 1.35f);
                viewer.playSound(hit, org.bukkit.Sound.ENTITY_PLAYER_ATTACK_CRIT, 0.9f, 0.85f);
            }
        }

        private static Particle parseParticleType(String name) {
            try {
                return Particle.valueOf(name.trim().toUpperCase());
            } catch (IllegalArgumentException ex) {
                return Particle.CRIT;
            }
        }

        private static double[] lerpOffset(double[] from, double[] to, double t) {
            return new double[]{
                    lerp(from.length > 0 ? from[0] : 0, to.length > 0 ? to[0] : 0, t),
                    lerp(from.length > 1 ? from[1] : 0, to.length > 1 ? to[1] : 0, t),
                    lerp(from.length > 2 ? from[2] : 0, to.length > 2 ? to[2] : 0, t)
            };
        }

        private static double lerp(double from, double to, double t) {
            return from + (to - from) * t;
        }

        private static double easeInOut(double t) {
            return t < 0.5 ? 2 * t * t : 1 - Math.pow(-2 * t + 2, 2) / 2;
        }

        private void spawnActor(CutsceneStep.Spawn spawn, IntConsumer onComplete) {
            if (actors.containsKey(spawn.actorId())) {
                despawnActor(spawn.actorId());
            }

            Location spawnLoc = offsetLocation(spawn.offset());
            spawnLoc.setYaw(anchor.getYaw() + spawn.yaw());
            spawnLoc.setPitch(0);

            Zombie zombie = spawnLoc.getWorld().spawn(spawnLoc, Zombie.class, entity -> {
                entity.setBaby(false);
                entity.setSilent(true);
                entity.setInvulnerable(true);
                entity.setCollidable(false);
                entity.setCanPickupItems(false);
                entity.setRemoveWhenFarAway(false);
                entity.setShouldBurnInDay(false);
                entity.setAI(false);
                entity.setInvisible(true);
                entity.setCustomNameVisible(false);
                entity.setMetadata(METADATA_KEY, new FixedMetadataValue(plugin, spawn.actorId()));
                entity.addScoreboardTag(ACTOR_TAG);
                Attribute speed = Attribute.GENERIC_MOVEMENT_SPEED;
                if (entity.getAttribute(speed) != null) {
                    entity.getAttribute(speed).setBaseValue(0);
                }
            });

            String modelId = spawn.model() != null ? spawn.model() : minionManager.getConfig().getModelId();
            List<String> fallbacks = minionManager.getConfig().getModelFallbackIds();
            ModelEngineBridge.BossModel model = modelEngine.attachMinionModel(
                    zombie, modelId, fallbacks, 1.0, 1.0);
            CutsceneActor actor = new CutsceneActor(spawn.actorId(), zombie, model);
            actors.put(spawn.actorId(), actor);

            Runnable finish = () -> {
                modelEngine.setBaseEntityVisible(model, zombie, true, 48.0);
                zombie.setInvisible(false);
                onComplete.accept(1);
            };

            if (spawn.preset() != null && !spawn.preset().isBlank()) {
                applyPresetSkin(spawn.preset(), model, zombie, finish);
            } else if (spawn.skin() != null && !spawn.skin().isBlank()) {
                modelEngine.applyPlayerSkin(model, zombie, spawn.skin(), 48.0, limbs -> finish.run());
            } else {
                finish.run();
            }
        }

        private void applyPresetSkin(String presetId, ModelEngineBridge.BossModel model, Zombie zombie, Runnable finish) {
            if (!minionManager.getConfig().hasPreset(presetId)) {
                plugin.getLogger().warning("컷신 프리셋 없음: " + presetId);
                finish.run();
                return;
            }
            MinionPreset preset = minionManager.getConfig().getPreset(presetId);
            if (preset.getSkinFile() != null && !preset.getSkinFile().isBlank()) {
                SkinTexturesUtil.loadTexturesProperty(plugin, preset.getSkinFile(), preset.getDisplayName())
                        .ifPresentOrElse(textures -> modelEngine.applyPlayerSkinTextures(
                                        model, zombie, preset.getDisplayName(), textures, 48.0, limbs -> finish.run()),
                                finish);
            } else {
                modelEngine.applyPlayerSkin(model, zombie, preset.getSkinUsername(), 48.0, limbs -> finish.run());
            }
        }

        private void moveActor(CutsceneStep.Move move, Runnable onComplete) {
            CutsceneActor actor = actors.get(move.actorId());
            if (actor == null || !actor.isValid()) {
                onComplete.run();
                return;
            }

            Location from = actor.getLocation().clone();
            Location to = offsetLocation(move.toOffset());
            to.setYaw(from.getYaw());
            to.setPitch(from.getPitch());

            if (move.animation() != null && !move.animation().isBlank() && actor.getModel() != null) {
                modelEngine.playLoopAnimation(actor.getModel(), move.animation(), 0.1, 0.1);
            }

            int duration = Math.max(1, move.durationTicks());
            final int[] tick = {0};
            stepTask = new org.bukkit.scheduler.BukkitRunnable() {
                @Override
                public void run() {
                    tick[0]++;
                    double t = Math.min(1.0, tick[0] / (double) duration);
                    Location next = interpolate(from, to, t);
                    actor.getEntity().teleport(next);
                    if (actor.getModel() != null) {
                        modelEngine.syncBodyRotation(actor.getModel(), next.getYaw());
                    }
                    if (tick[0] >= duration) {
                        cancel();
                        if (actor.getModel() != null && move.animation() != null) {
                            modelEngine.stopAnimation(actor.getModel(), move.animation());
                        }
                        onComplete.run();
                    }
                }
            }.runTaskTimer(plugin, 0L, 1L);
        }

        private void playActorAnimation(CutsceneStep.Animate animate) {
            CutsceneActor actor = actors.get(animate.actorId());
            if (actor == null || actor.getModel() == null) {
                return;
            }
            if (animate.loop()) {
                modelEngine.playLoopAnimation(actor.getModel(), animate.animation(), 0.1, 0.1);
            } else {
                modelEngine.playAnimation(actor.getModel(), animate.animation(), 0.1, 0.1, false);
            }
            LivingEntity entity = actor.getEntity();
            entity.getWorld().spawnParticle(Particle.SWEEP_ATTACK,
                    entity.getLocation().add(0, 1.0, 0), 1, 0, 0, 0, 0);
        }

        private void stopActorAnimation(CutsceneStep.StopAnimation stop) {
            CutsceneActor actor = actors.get(stop.actorId());
            if (actor == null || actor.getModel() == null) {
                return;
            }
            if (stop.animation() != null && !stop.animation().isBlank()) {
                modelEngine.stopAnimation(actor.getModel(), stop.animation());
            }
        }

        private void despawnActor(String actorId) {
            if ("all".equalsIgnoreCase(actorId)) {
                cleanupActors();
                return;
            }
            CutsceneActor actor = actors.remove(actorId);
            if (actor != null) {
                actor.remove(modelEngine);
            }
        }

        private void cleanupActors() {
            for (CutsceneActor actor : actors.values()) {
                actor.remove(modelEngine);
            }
            actors.clear();
        }

        private Location offsetLocation(double[] offset) {
            double yawRad = Math.toRadians(anchor.getYaw());
            double cos = Math.cos(yawRad);
            double sin = Math.sin(yawRad);
            double dx = offset.length > 0 ? offset[0] : 0;
            double dy = offset.length > 1 ? offset[1] : 0;
            double dz = offset.length > 2 ? offset[2] : 0;
            double rx = dx * cos - dz * sin;
            double rz = dx * sin + dz * cos;
            return anchor.clone().add(rx, dy, rz);
        }

        private static Location interpolate(Location from, Location to, double t) {
            Vector delta = to.toVector().subtract(from.toVector());
            Location result = from.clone().add(delta.multiply(t));
            result.setYaw(lerpAngle(from.getYaw(), to.getYaw(), (float) t));
            return result;
        }

        private static float lerpAngle(float from, float to, float t) {
            float diff = wrapDegrees(to - from);
            return from + diff * t;
        }

        private static float wrapDegrees(float angle) {
            float wrapped = angle % 360f;
            if (wrapped >= 180f) {
                wrapped -= 360f;
            }
            if (wrapped < -180f) {
                wrapped += 360f;
            }
            return wrapped;
        }

        private static void faceLocation(Location from, Location target) {
            Vector direction = target.toVector().subtract(from.toVector());
            if (direction.lengthSquared() < 1.0E-6) {
                return;
            }
            direction.normalize();
            Location temp = from.clone();
            temp.setDirection(direction);
            from.setYaw(temp.getYaw());
            from.setPitch(temp.getPitch());
        }

        private void schedule(Runnable onComplete, int ticks) {
            stepTask = Bukkit.getScheduler().runTaskLater(plugin, () -> {
                if (sessions.containsKey(viewer.getUniqueId())) {
                    onComplete.run();
                }
            }, Math.max(1, ticks));
        }

        private void cancelTasks() {
            if (stepTask != null) {
                stepTask.cancel();
                stepTask = null;
            }
            if (cameraTask != null) {
                cameraTask.cancel();
                cameraTask = null;
            }
            stopFireParticles();
            burningActors.clear();
        }
    }
}
