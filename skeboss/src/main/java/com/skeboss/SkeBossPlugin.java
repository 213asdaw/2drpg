package com.skeboss;

import com.skeboss.boss.ModelEngineBossService;
import com.skeboss.command.SkeBossCommand;
import com.skeboss.listener.SkeBossListener;
import org.bukkit.plugin.java.JavaPlugin;

public final class SkeBossPlugin extends JavaPlugin {

    private static SkeBossPlugin instance;
    private ModelEngineBossService bossService;

    @Override
    public void onEnable() {
        instance = this;
        saveDefaultConfig();

        if (!getServer().getPluginManager().isPluginEnabled("ModelEngine")) {
            getLogger().severe("ModelEngine 플러그인이 없습니다. SkeBoss를 비활성화합니다.");
            getServer().getPluginManager().disablePlugin(this);
            return;
        }

        bossService = new ModelEngineBossService(this);
        getCommand("skeboss").setExecutor(new SkeBossCommand(bossService));
        getServer().getPluginManager().registerEvents(new SkeBossListener(bossService), this);

        getLogger().info("SkeBoss 활성화 완료");
    }

    @Override
    public void onDisable() {
        if (bossService != null) {
            bossService.removeAll();
        }
    }

    public static SkeBossPlugin getInstance() {
        return instance;
    }

    public ModelEngineBossService getBossService() {
        return bossService;
    }
}
