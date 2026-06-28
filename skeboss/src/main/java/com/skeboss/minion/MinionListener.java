package com.skeboss.minion;

import com.skeboss.modelengine.ModelEngineBridge;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.event.EventHandler;
import org.bukkit.event.EventPriority;
import org.bukkit.event.Listener;
import org.bukkit.event.entity.EntityCombustEvent;
import org.bukkit.event.entity.EntityDamageEvent;
import org.bukkit.event.entity.EntityDeathEvent;
import org.bukkit.event.player.PlayerJoinEvent;
import org.bukkit.event.player.PlayerQuitEvent;
import org.bukkit.event.world.ChunkLoadEvent;

public final class MinionListener implements Listener {

    private final MinionManager minionManager;

    public MinionListener(MinionManager minionManager) {
        this.minionManager = minionManager;
    }

    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = true)
    public void onCombust(EntityCombustEvent event) {
        if (!minionManager.getConfig().isFireImmune()) {
            return;
        }
        if (event.getEntity() instanceof LivingEntity living && minionManager.isMinion(living)) {
            event.setCancelled(true);
            living.setFireTicks(0);
        }
    }

    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = true)
    public void onFireDamage(EntityDamageEvent event) {
        if (!minionManager.getConfig().isFireImmune()) {
            return;
        }
        if (!(event.getEntity() instanceof LivingEntity living) || !minionManager.isMinion(living)) {
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

    @EventHandler
    public void onDeath(EntityDeathEvent event) {
        LivingEntity entity = event.getEntity();
        if (!minionManager.isMinion(entity)) {
            return;
        }

        event.getDrops().clear();
        event.setDroppedExp(0);

        SkeMinion minion = minionManager.getMinion(entity.getUniqueId());
        if (minion == null) {
            return;
        }
        minionManager.onMinionDeath(minion);
        minionManager.remove(minion);
    }

    @EventHandler
    public void onJoin(PlayerJoinEvent event) {
        Player player = event.getPlayer();
        MinionConfig config = minionManager.getConfig();
        double radiusSq = config.getViewerSyncRadius() * config.getViewerSyncRadius();

        for (SkeMinion minion : minionManager.getMinions()) {
            LivingEntity entity = minion.getEntity();
            ModelEngineBridge.BossModel model = minion.getModel();
            if (model == null || !entity.getWorld().equals(player.getWorld())) {
                continue;
            }
            if (entity.getLocation().distanceSquared(player.getLocation()) <= radiusSq) {
                minionManager.syncMinionViewers(minion, player);
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
        MinionConfig config = minionManager.getConfig();
        double radiusSq = config.getViewerSyncRadius() * config.getViewerSyncRadius();

        for (SkeMinion minion : minionManager.getMinions()) {
            LivingEntity entity = minion.getEntity();
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
                    minionManager.getModelEngine().syncNearbyPlayers(model, entity, config.getViewerSyncRadius());
                }
            }
        }
    }
}
