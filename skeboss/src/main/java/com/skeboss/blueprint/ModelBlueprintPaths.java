package com.skeboss.blueprint;

import org.bukkit.plugin.java.JavaPlugin;

import java.nio.file.Files;
import java.nio.file.Path;

/** ModelEngine blueprints 폴더 경로 */
public final class ModelBlueprintPaths {

    private ModelBlueprintPaths() {
    }

    public static Path blueprintPath(JavaPlugin plugin, String modelId) {
        return plugin.getDataFolder().getParentFile().toPath()
                .resolve("ModelEngine")
                .resolve("blueprints")
                .resolve(modelId + ".bbmodel");
    }

    public static boolean isInstalled(JavaPlugin plugin, String modelId) {
        return Files.isRegularFile(blueprintPath(plugin, modelId));
    }
}
