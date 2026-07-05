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
            Location cameraLoc = offsetLocation(camera.offset());
            if (camera.lookAtActor() != null && !camera.lookAtActor().isBlank()) {
                CutsceneActor actor = actors.get(camera.lookAtActor());
                if (actor != null && actor.isValid()) {
                    faceLocation(cameraLoc, actor.getLocation().clone().add(0, 1.4, 0));
                }
            } else if (camera.yaw() != null && camera.pitch() != null) {
                cameraLoc.setYaw(camera.yaw());
                cameraLoc.setPitch(camera.pitch());
            }
            teleportViewer(cameraLoc);

            if (camera.hold()) {
                final Location held = cameraLoc.clone();
                final String lookAt = camera.lookAtActor();
                cameraTask = Bukkit.getScheduler().runTaskTimer(plugin, () -> {
                    if (!viewer.isOnline()) {
                        return;
                    }
                    Location next = held.clone();
                    if (lookAt != null && !lookAt.isBlank()) {
                        CutsceneActor actor = actors.get(lookAt);
                        if (actor != null && actor.isValid()) {
                            faceLocation(next, actor.getLocation().clone().add(0, 1.4, 0));
                        }
                    }
                    teleportViewer(next);
                }, 1L, 1L);
            }
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
        }
    }
}
