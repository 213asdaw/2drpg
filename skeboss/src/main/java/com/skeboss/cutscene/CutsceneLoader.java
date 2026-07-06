package com.skeboss.cutscene;

import org.bukkit.configuration.ConfigurationSection;
import org.bukkit.configuration.file.YamlConfiguration;
import org.bukkit.plugin.Plugin;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.logging.Level;

public final class CutsceneLoader {

    private final Plugin plugin;

    public CutsceneLoader(Plugin plugin) {
        this.plugin = plugin;
    }

    public Map<String, CutsceneDefinition> loadAll() {
        File folder = cutsceneFolder();
        if (!folder.exists()) {
            folder.mkdirs();
            saveDefaultExamples(folder);
        }

        Map<String, CutsceneDefinition> scenes = new LinkedHashMap<>();
        File[] files = folder.listFiles((dir, name) -> name.endsWith(".yml") || name.endsWith(".yaml"));
        if (files == null) {
            return scenes;
        }

        for (File file : files) {
            try {
                CutsceneDefinition definition = loadFile(file);
                scenes.put(definition.getId(), definition);
            } catch (Exception ex) {
                plugin.getLogger().log(Level.WARNING, "컷신 로드 실패: " + file.getName(), ex);
            }
        }
        return scenes;
    }

    public CutsceneDefinition loadFile(File file) {
        YamlConfiguration yaml = YamlConfiguration.loadConfiguration(file);
        String id = yaml.getString("id", file.getName().replaceFirst("\\.ya?ml$", ""));
        String description = yaml.getString("description", "");
        List<?> rawSteps = yaml.getList("steps");
        if (rawSteps == null || rawSteps.isEmpty()) {
            throw new IllegalArgumentException("steps 가 비어 있습니다: " + file.getName());
        }

        List<CutsceneStep> steps = new ArrayList<>();
        for (Object raw : rawSteps) {
            if (raw instanceof ConfigurationSection section) {
                steps.add(CutsceneStep.parse(section.getValues(false)));
            } else if (raw instanceof Map<?, ?> map) {
                steps.add(CutsceneStep.parse(map));
            } else {
                throw new IllegalArgumentException("잘못된 스텝 형식: " + raw);
            }
        }
        return new CutsceneDefinition(id, description, steps);
    }

    private File cutsceneFolder() {
        return new File(plugin.getDataFolder(), "cutscenes");
    }

    private void saveDefaultExamples(File folder) {
        saveResourceIfMissing(folder, "tutorial_fight.yml");
        saveResourceIfMissing(folder, "burning_brawl.yml");
    }

    private void saveResourceIfMissing(File folder, String name) {
        File target = new File(folder, name);
        if (target.exists()) {
            return;
        }
        try {
            plugin.saveResource("cutscenes/" + name, false);
        } catch (IllegalArgumentException ex) {
            plugin.getLogger().warning("기본 컷신 없음: " + name);
        }
    }
}
