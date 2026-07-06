package com.skeboss.minion;

import org.bukkit.plugin.java.JavaPlugin;

import java.io.IOException;
import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;

/** ModelEngine player limb blueprint (player_model.bbmodel) 설치 */
public final class MinionBlueprintInstaller {

    public static final String MODEL_ID = "player_model";
    private static final String RESOURCE_PATH = "blueprints/player_model.bbmodel";

    private MinionBlueprintInstaller() {
    }

    public static Path blueprintPath(JavaPlugin plugin) {
        return plugin.getDataFolder().getParentFile().toPath()
                .resolve("ModelEngine")
                .resolve("blueprints")
                .resolve(MODEL_ID + ".bbmodel");
    }

    public static boolean isInstalled(JavaPlugin plugin) {
        return Files.isRegularFile(blueprintPath(plugin));
    }

    public static void install(JavaPlugin plugin) throws IOException {
        Path target = blueprintPath(plugin);
        Files.createDirectories(target.getParent());
        try (InputStream input = plugin.getResource(RESOURCE_PATH)) {
            if (input == null) {
                throw new IOException("플러그인 JAR에 " + RESOURCE_PATH + " 가 없습니다.");
            }
            Files.copy(input, target, StandardCopyOption.REPLACE_EXISTING);
        }
    }
}
