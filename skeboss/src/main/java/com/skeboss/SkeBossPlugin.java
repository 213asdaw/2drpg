package com.skeboss;

import com.skeboss.boss.BossAI;
import com.skeboss.boss.BossManager;
import com.skeboss.command.SkeBossCommand;
import com.skeboss.listener.BossListener;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.skript.SkriptBridge;
import org.bukkit.plugin.java.JavaPlugin;

public final class SkeBossPlugin extends JavaPlugin {

    private static SkeBossPlugin instance;

    private ModelEngineBridge modelEngine;
    private BossManager bossManager;
    private BossAI bossAI;

    @Override
    public void onEnable() {
        instance = this;
        saveDefaultConfig();

        if (!getServer().getPluginManager().isPluginEnabled("ModelEngine")) {
            getLogger().severe("ModelEngine이 없습니다. plugins/ModelEngine.jar 를 넣고 재시작하세요.");
            getServer().getPluginManager().disablePlugin(this);
            return;
        }

        modelEngine = new ModelEngineBridge(this);
        if (!modelEngine.isAvailable()) {
            getServer().getPluginManager().disablePlugin(this);
            return;
        }

        SkriptBridge skriptBridge = new SkriptBridge(this);
        bossManager = new BossManager(this, modelEngine, skriptBridge);
        bossAI = new BossAI(this, bossManager);
        bossAI.start();

        SkeBossCommand command = new SkeBossCommand(bossManager);
        getCommand("skeboss").setExecutor(command);
        getCommand("skeboss").setTabCompleter(command);
        getServer().getPluginManager().registerEvents(new BossListener(bossManager), this);

        getLogger().info("SkeBoss v1.0 활성화 — /skeboss spawn");
    }

    @Override
    public void onDisable() {
        if (bossManager != null) {
            bossManager.removeAll();
        }
    }

    public static SkeBossPlugin getInstance() {
        return instance;
    }

    public BossManager getBossManager() {
        return bossManager;
    }
}
