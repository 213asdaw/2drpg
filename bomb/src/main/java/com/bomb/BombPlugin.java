package com.bomb;

import com.bomb.command.BombCommand;
import com.bomb.item.BombManager;
import com.bomb.listener.BombListener;
import org.bukkit.plugin.java.JavaPlugin;

public final class BombPlugin extends JavaPlugin {

    private BombManager bombManager;

    @Override
    public void onEnable() {
        saveDefaultConfig();
        bombManager = new BombManager(this);

        BombCommand bombCommand = new BombCommand(bombManager);
        var cmd = getCommand("bomb");
        if (cmd != null) {
            cmd.setExecutor(bombCommand);
            cmd.setTabCompleter(bombCommand);
        }

        getServer().getPluginManager().registerEvents(new BombListener(bombManager), this);
        getLogger().info("Bomb 활성화 — 폭탄마의 폭탄, 블레이즈 막대 CMD 1003 투사체");
    }

    @Override
    public void onDisable() {
        getLogger().info("Bomb 비활성화");
    }

    public BombManager getBombManager() {
        return bombManager;
    }
}
