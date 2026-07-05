package com.skeboss.cutscene;

import org.bukkit.entity.Player;
import org.bukkit.event.Event;
import org.bukkit.event.HandlerList;
import org.bukkit.plugin.Plugin;

public final class CutsceneEndEvent extends Event {

    private static final HandlerList HANDLERS = new HandlerList();

    private final Player player;
    private final String cutsceneId;
    private final boolean completed;

    public CutsceneEndEvent(Player player, String cutsceneId, boolean completed) {
        this.player = player;
        this.cutsceneId = cutsceneId;
        this.completed = completed;
    }

    public Player getPlayer() {
        return player;
    }

    public String getCutsceneId() {
        return cutsceneId;
    }

    public boolean isCompleted() {
        return completed;
    }

    @Override
    public HandlerList getHandlers() {
        return HANDLERS;
    }

    public static HandlerList getHandlerList() {
        return HANDLERS;
    }

    public static void fire(Plugin plugin, Player player, String cutsceneId, boolean completed) {
        plugin.getServer().getPluginManager().callEvent(new CutsceneEndEvent(player, cutsceneId, completed));
    }
}
