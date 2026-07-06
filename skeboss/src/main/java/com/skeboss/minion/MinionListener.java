package com.skeboss.minion;

import com.skeboss.modelengine.ModelEngineBridge;
import org.bukkit.Location;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.entity.Projectile;
import org.bukkit.event.EventHandler;
import org.bukkit.event.EventPriority;
import org.bukkit.event.Listener;
import org.bukkit.event.entity.EntityCombustEvent;
import org.bukkit.event.entity.EntityDamageByEntityEvent;
import org.bukkit.event.entity.EntityDamageEvent;
import org.bukkit.event.entity.EntityDeathEvent;
import org.bukkit.event.player.PlayerJoinEvent;
import org.bukkit.event.player.PlayerQuitEvent;
import org.bukkit.event.world.ChunkLoadEvent;
import org.bukkit.projectiles.ProjectileSource;

public final class MinionListener implements Listener {

    private final MinionManager minionManager;

    public MinionListener(MinionManager minionManager) {
        this.minionManager = minionManager;
    }

    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = true)
    public void onCombust(EntityCombustEvent event) {
        if (!(event.getEntity() instanceof LivingEntity living) || !minionManager.isMinion(living)) {
            return;
        }
        SkeMinion minion = minionManager.resolveMinion(living);
        if (minion == null || !minion.getPreset().isFireImmune()) {
            return;
        }
        event.setCancelled(true);
        living.setFireTicks(0);
    }

    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = true)
    public void onFireDamage(EntityDamageEvent event) {
        if (!(event.getEntity() instanceof LivingEntity living) || !minionManager.isMinion(living)) {
            return;
        }
        SkeMinion minion = minionManager.resolveMinion(living);
        if (minion == null || !minion.getPreset().isFireImmune()) {
            return;
        }
        EntityDamageEvent.DamageCause cause = event.getCause();
        if (cause == EntityDamageEvent.DamageCause.FIRE
                || cause == EntityDamageEvent.DamageCause.FIRE_TICK
                || cause == EntityDamageEvent.DamageCause.LAVA
                || cause == EntityDamageEvent.DamageCause.HOT_FLOOR) {
            event.setCancelled(true);
            living.setFireTicks(0);
        }
    }

    @EventHandler(priority = EventPriority.MONITOR, ignoreCancelled = true)
    public void onDamageByEntity(EntityDamageByEntityEvent event) {
        if (!(event.getEntity() instanceof LivingEntity victim) || !minionManager.isMinion(victim)) {
            return;
        }
        SkeMinion minion = minionManager.resolveMinion(victim);
        if (minion == null) {
            minion = minionManager.getMinion(victim.getUniqueId());
        }
        if (minion == null) {
            return;
        }
        if (!minion.getPreset().isBacklineMode()) {
            return;
        }

        Player attacker = resolvePlayerAttacker(event.getDamager());
        if (attacker != null) {
            minion.provoke(attacker);
        }
    }

    @EventHandler(priority = EventPriority.MONITOR)
    public void onDeath(EntityDeathEvent event) {
        LivingEntity entity = event.getEntity();
        if (!minionManager.isMinion(entity)) {
            return;
        }

        event.getDrops().clear();
        event.setDroppedExp(0);

        SkeMinion minion = minionManager.resolveMinion(entity);
        if (minion == null) {
            minion = minionManager.getMinion(entity.getUniqueId());
        }
        if (minion == null) {
            return;
        }
        minionManager.onMinionDeath(minion);
        minionManager.remove(minion);
    }

    @EventHandler
    public void onJoin(PlayerJoinEvent event) {
        Player player = event.getPlayer();
        for (SkeMinion minion : minionManager.getMinions()) {
            LivingEntity entity = minion.getEntity();
            double radiusSq = minion.getPreset().getViewerSyncRadius() * minion.getPreset().getViewerSyncRadius();
            ModelEngineBridge.BossModel model = minion.getModel();
            if (!entity.getWorld().equals(player.getWorld())) {
                continue;
            }
            if (entity.getLocation().distanceSquared(player.getLocation()) <= radiusSq) {
                minionManager.syncMinionViewers(minion, player);
                minion.addViewer(player);
            }
        }
    }

    @EventHandler
    public void onQuit(PlayerQuitEvent event) {
        Player player = event.getPlayer();
        for (SkeMinion minion : minionManager.getMinions()) {
            minion.removeViewer(player);
        }
    }

    @EventHandler
    public void onChunkLoad(ChunkLoadEvent event) {
        for (MinionSpawner spawner : minionManager.getSpawnerStorage().all()) {
            Location spawnerLoc = spawner.toLocation();
            if (spawnerLoc == null || !spawnerLoc.getWorld().equals(event.getWorld())) {
                continue;
            }
            if (event.getChunk().equals(spawnerLoc.getChunk())) {
                minionManager.ensureSpawnerMinion(spawner);
            }
        }

        for (SkeMinion minion : minionManager.getMinions()) {
            LivingEntity entity = minion.getEntity();
            double radiusSq = minion.getPreset().getViewerSyncRadius() * minion.getPreset().getViewerSyncRadius();
            if (!entity.getWorld().equals(event.getWorld())) {
                continue;
            }
            if (!event.getChunk().equals(entity.getLocation().getChunk())) {
                continue;
            }
            ModelEngineBridge.BossModel model = minion.getModel();
            if (model == null) {
                continue;
            }
            for (Player player : event.getWorld().getPlayers()) {
                if (player.getLocation().distanceSquared(entity.getLocation()) <= radiusSq) {
                    minionManager.getModelEngine().syncNearbyPlayers(model, entity, minion.getPreset().getViewerSyncRadius());
                }
            }
        }
    }

    private static Player resolvePlayerAttacker(org.bukkit.entity.Entity damager) {
        if (damager instanceof Player player) {
            return player;
        }
        if (damager instanceof Projectile projectile) {
            ProjectileSource source = projectile.getShooter();
            if (source instanceof Player player) {
                return player;
            }
        }
        return null;
    }
}
