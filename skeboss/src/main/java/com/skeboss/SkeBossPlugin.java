package com.skeboss;

import com.skeboss.boss.BossAI;
import com.skeboss.boss.BossManager;
import com.skeboss.command.SkeBossCommand;
import com.skeboss.listener.BossListener;
import com.skeboss.listener.WeaponItemGuard;
import com.skeboss.listener.WeaponListener;
import com.skeboss.minion.MinionAI;
import com.skeboss.minion.MinionListener;
import com.skeboss.minion.MinionManager;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.skript.SkriptBridge;
import com.skeboss.weapon.WeaponManager;
import org.bukkit.plugin.java.JavaPlugin;

public final class SkeBossPlugin extends JavaPlugin {

    private static SkeBossPlugin instance;

    private ModelEngineBridge modelEngine;
    private BossManager bossManager;
    private MinionManager minionManager;
    private WeaponManager weaponManager;
    private BossAI bossAI;
    private MinionAI minionAI;

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
        minionManager = new MinionManager(this, modelEngine);
        weaponManager = new WeaponManager(this, bossManager, skriptBridge);
        bossAI = new BossAI(this, bossManager);
        bossAI.start();
        minionAI = new MinionAI(this, minionManager);
        minionAI.start();
        minionManager.startupSpawners();

        SkeBossCommand command = new SkeBossCommand(bossManager, minionManager, weaponManager);
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
        getServer().getPluginManager().registerEvents(new MinionListener(minionManager), this);
        getServer().getPluginManager().registerEvents(new WeaponListener(weaponManager), this);
        getServer().getPluginManager().registerEvents(new WeaponItemGuard(this, weaponManager), this);

        getLogger().info("SkeBoss 활성화 (minion-skin-v2) — /skeboss help | /skeweapon | /skeboss minion spawner");
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
        if (minionManager != null) {
            minionManager.removeAll();
        }
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

    public MinionManager getMinionManager() {
        return minionManager;
    }

    public WeaponManager getWeaponManager() {
        return weaponManager;
    }
}
