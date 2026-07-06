package com.skeboss.cutscene;

import com.skeboss.SkeBossPlugin;
import org.bukkit.entity.Player;
import org.bukkit.event.EventHandler;
import org.bukkit.event.EventPriority;
import org.bukkit.event.Listener;
import org.bukkit.event.entity.EntityDamageEvent;
import org.bukkit.event.player.PlayerMoveEvent;
import org.bukkit.event.player.PlayerQuitEvent;

public final class CutsceneListener implements Listener {

    private final SkeBossPlugin plugin;
    private final CutsceneManager cutsceneManager;

    public CutsceneListener(SkeBossPlugin plugin, CutsceneManager cutsceneManager) {
        this.plugin = plugin;
        this.cutsceneManager = cutsceneManager;
    }

    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = true)
    public void onMove(PlayerMoveEvent event) {
        Player player = event.getPlayer();
        if (!cutsceneManager.isPlaying(player)) {
            return;
        }
        if (event.getFrom().getX() == event.getTo().getX()
                && event.getFrom().getY() == event.getTo().getY()
                && event.getFrom().getZ() == event.getTo().getZ()) {
            return;
        }
        if (cutsceneManager.shouldBypassMoveLock(player)) {
            return;
        }
        event.setTo(event.getFrom());
    }

    @EventHandler(priority = EventPriority.HIGH, ignoreCancelled = true)
    public void onDamage(EntityDamageEvent event) {
        if (event.getEntity() instanceof Player player && cutsceneManager.isPlaying(player)) {
            event.setCancelled(true);
        }
    }

    @EventHandler
    public void onQuit(PlayerQuitEvent event) {
        if (cutsceneManager.isPlaying(event.getPlayer())) {
            cutsceneManager.stop(event.getPlayer(), false);
        }
    }
}
