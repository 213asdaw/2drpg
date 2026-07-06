package com.skeboss.blueprint;

import org.bukkit.plugin.java.JavaPlugin;

import java.io.IOException;
import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;

/** ModelEngine 보스 blueprint (bombboss.bbmodel 등) 설치 */
public final class BossBlueprintInstaller {

    private BossBlueprintInstaller() {
    }

    public static Path blueprintPath(JavaPlugin plugin, String modelId) {
        return ModelBlueprintPaths.blueprintPath(plugin, modelId);
    }

    public static boolean isInstalled(JavaPlugin plugin, String modelId) {
        return ModelBlueprintPaths.isInstalled(plugin, modelId);
    }

    public static void install(JavaPlugin plugin, String modelId) throws IOException {
        String resourcePath = "blueprints/" + modelId + ".bbmodel";
        Path target = blueprintPath(plugin, modelId);
        Files.createDirectories(target.getParent());
        try (InputStream input = plugin.getResource(resourcePath)) {
            if (input == null) {
                throw new IOException("플러그인 JAR에 " + resourcePath + " 가 없습니다.");
            }
            Files.copy(input, target, StandardCopyOption.REPLACE_EXISTING);
        }
    }
}
