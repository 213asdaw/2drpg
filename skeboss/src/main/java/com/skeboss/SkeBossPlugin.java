package com.skeboss;

import com.skeboss.boss.BossAI;
import com.skeboss.boss.BossManager;
import com.skeboss.command.SkeBossCommand;
import com.skeboss.listener.BossListener;
import com.skeboss.listener.WeaponListener;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.skript.SkriptBridge;
import com.skeboss.weapon.WeaponManager;
import org.bukkit.plugin.java.JavaPlugin;

public final class SkeBossPlugin extends JavaPlugin {

    private static SkeBossPlugin instance;

    private ModelEngineBridge modelEngine;
    private BossManager bossManager;
    private WeaponManager weaponManager;
    private BossAI bossAI;

    @Override
    public void onEnable() {
        instance = this;
        saveDefaultConfig();
        mergeDefaultConfig();

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
        weaponManager = new WeaponManager(this, bossManager, skriptBridge);
        bossAI = new BossAI(this, bossManager);
        bossAI.start();

        SkeBossCommand command = new SkeBossCommand(bossManager, weaponManager);
        var skebossCmd = getCommand("skeboss");
        if (skebossCmd == null) {
            getLogger().severe("plugin.yml에 skeboss 명령이 없습니다.");
            getServer().getPluginManager().disablePlugin(this);
            return;
        }
        skebossCmd.setExecutor(command);
        skebossCmd.setTabCompleter(command);

        var skeWeaponCmd = getCommand("skeweapon");
        if (skeWeaponCmd != null) {
            skeWeaponCmd.setExecutor(command);
            skeWeaponCmd.setTabCompleter(command);
        }

        getServer().getPluginManager().registerEvents(new BossListener(bossManager), this);
        getServer().getPluginManager().registerEvents(new WeaponListener(weaponManager), this);

        getLogger().info("SkeBoss 활성화 — /skeboss help | /skeweapon | /skeboss weapon artificial-arm");
    }

    public void mergeAndReloadConfig() {
        mergeDefaultConfig();
    }

    private void mergeDefaultConfig() {
        reloadConfig();
        getConfig().options().copyDefaults(true);
        saveConfig();
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

    public WeaponManager getWeaponManager() {
        return weaponManager;
    }
}
