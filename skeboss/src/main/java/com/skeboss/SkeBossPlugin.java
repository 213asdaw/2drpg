package com.skeboss;

import com.skeboss.boss.BossAI;
import com.skeboss.boss.BossManager;
import com.skeboss.coin.CoinManager;
import com.skeboss.command.SkeBossCommand;
import com.skeboss.cutscene.CutsceneListener;
import com.skeboss.cutscene.CutsceneManager;
import com.skeboss.listener.BossBombListener;
import com.skeboss.listener.BossListener;
import com.skeboss.listener.CoinListener;
import com.skeboss.listener.WeaponItemGuard;
import com.skeboss.listener.WeaponListener;
import com.skeboss.minion.MinionAI;
import com.skeboss.minion.MinionListener;
import com.skeboss.minion.MinionManager;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.skript.SkriptBridge;
import com.skeboss.weapon.WeaponManager;
import org.bukkit.Bukkit;
import org.bukkit.entity.Player;
import org.bukkit.plugin.java.JavaPlugin;

import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.List;

import org.bukkit.configuration.ConfigurationSection;
import org.bukkit.configuration.file.YamlConfiguration;

public final class SkeBossPlugin extends JavaPlugin {

    private static SkeBossPlugin instance;

    private ModelEngineBridge modelEngine;
    private BossManager bossManager;
    private MinionManager minionManager;
    private WeaponManager weaponManager;
    private CoinManager coinManager;
    private CutsceneManager cutsceneManager;
    private BossAI bossAI;
    private MinionAI minionAI;

    @Override
    public void onEnable() {
        instance = this;
        saveDefaultConfig();
        mergeDefaultConfig();
        if (!getDataFolder().exists()) {
            getDataFolder().mkdirs();
        }
        new java.io.File(getDataFolder(), "skins").mkdirs();

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
        coinManager = new CoinManager(this);
        cutsceneManager = new CutsceneManager(this, modelEngine, minionManager);
        bossAI = new BossAI(this, bossManager);
        bossAI.start();
        minionAI = new MinionAI(this, minionManager);
        minionAI.start();

        getServer().getPluginManager().registerEvents(new BossListener(bossManager), this);
        getServer().getPluginManager().registerEvents(new BossBombListener(), this);
        getServer().getPluginManager().registerEvents(new MinionListener(minionManager), this);
        getServer().getPluginManager().registerEvents(new WeaponListener(weaponManager), this);
        getServer().getPluginManager().registerEvents(new WeaponItemGuard(this, weaponManager), this);
        getServer().getPluginManager().registerEvents(new CoinListener(coinManager), this);
        getServer().getPluginManager().registerEvents(new CutsceneListener(this, cutsceneManager), this);

        modelEngine.preloadPlayerSkin(getConfig().getString("minion.presets.emp4348.skin-username",
                getConfig().getString("minion.skin-username", "EMP4348")));
        for (String presetId : bossManager.getPresetRegistry().getPresetIds()) {
            try {
                var preset = bossManager.getPresetRegistry().load(this, presetId);
                if (preset.usesPlayerSkin()) {
                    modelEngine.preloadPlayerSkin(preset.getSkinUsername());
                }
            } catch (IllegalArgumentException ignored) {
                // skip invalid preset
            }
        }
        minionManager.startupSpawners();

        SkeBossCommand command = new SkeBossCommand(bossManager, minionManager, weaponManager, coinManager, cutsceneManager);
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

        getLogger().info("SkeBoss 활성화 (통합) — 코어·불의검사·폭탄·코인·컷신");
    }

    public void mergeAndReloadConfig() {
        mergeDefaultConfig();
    }

    private void mergeDefaultConfig() {
        reloadConfig();
        getConfig().options().copyDefaults(true);
        migrateLegacyWeaponNames();
        migrateMissingBossPresets();
        saveConfig();
    }

    /** 서버 config에 예전 "인조 무기" 이름이 남아 있으면 코어로 맞춤 */
    private void migrateLegacyWeaponNames() {
        var arm = getConfig().getConfigurationSection("weapons.artificial-arm");
        if (arm == null) {
            return;
        }
        String display = arm.getString("display-name", "");
        String plain = org.bukkit.ChatColor.stripColor(
                display.replace('&', '§')
        );
        if (plain.contains("인조 무기") || plain.contains("인조무기")) {
            if (!plain.contains("코어")) {
                arm.set("display-name", "&6&l인조인간의 코어");
                getLogger().info("config 마이그레이션: artificial-arm 표시 이름 → 인조인간의 코어");
            }
        }
        List<String> keywords = arm.getStringList("name-keywords");
        if (keywords.isEmpty()) {
            arm.set("name-keywords", List.of(
                    "인조인간의 코어", "인조인간코어", "인조인간",
                    "핵심 동력원", "인조 무기", "인조무기"
            ));
        } else {
            List<String> merged = new java.util.ArrayList<>(keywords);
            for (String required : List.of("인조인간의 코어", "인조인간", "핵심 동력원")) {
                if (merged.stream().noneMatch(k -> k.equalsIgnoreCase(required))) {
                    merged.add(required);
                }
            }
            arm.set("name-keywords", merged);
        }
    }

    /** 예전 config에 boss-presets.bomb 등 신규 프리셋이 없으면 JAR 기본값으로 추가 */
    private void migrateMissingBossPresets() {
        try (var stream = getResource("config.yml")) {
            if (stream == null) {
                return;
            }
            YamlConfiguration defaults = YamlConfiguration.loadConfiguration(
                    new InputStreamReader(stream, StandardCharsets.UTF_8)
            );
            ConfigurationSection presets = defaults.getConfigurationSection("boss-presets");
            if (presets == null) {
                return;
            }
            for (String presetId : presets.getKeys(false)) {
                String path = "boss-presets." + presetId;
                if (getConfig().isConfigurationSection(path)) {
                    continue;
                }
                ConfigurationSection source = presets.getConfigurationSection(presetId);
                if (source == null) {
                    continue;
                }
                getConfig().createSection(path, source.getValues(true));
                getLogger().info("config 마이그레이션: boss-presets." + presetId + " 추가");
            }
        } catch (Exception ex) {
            getLogger().warning("보스 프리셋 마이그레이션 실패: " + ex.getMessage());
        }
    }

    @Override
    public void onDisable() {
        if (cutsceneManager != null) {
            for (Player player : Bukkit.getOnlinePlayers()) {
                if (cutsceneManager.isPlaying(player)) {
                    cutsceneManager.stop(player, false);
                }
            }
        }
        if (minionManager != null) {
            minionManager.shutdownPersist();
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

    public CoinManager getCoinManager() {
        return coinManager;
    }

    public CutsceneManager getCutsceneManager() {
        return cutsceneManager;
    }
}
