package com.skeboss.minion;

import com.skeboss.SkeBossPlugin;

import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Base64;
import java.util.Optional;
import java.util.UUID;

public final class SkinTexturesUtil {

    private SkinTexturesUtil() {
    }

    public static Optional<File> resolveSkinFile(SkeBossPlugin plugin, String skinFile) {
        if (skinFile == null || skinFile.isBlank()) {
            return Optional.empty();
        }
        File raw = new File(skinFile);
        if (raw.isAbsolute()) {
            return raw.isFile() ? Optional.of(raw) : Optional.empty();
        }
        File inData = new File(plugin.getDataFolder(), skinFile);
        if (inData.isFile()) {
            return Optional.of(inData);
        }
        File inSkins = new File(plugin.getDataFolder(), "skins/" + skinFile);
        if (inSkins.isFile()) {
            return Optional.of(inSkins);
        }
        return Optional.empty();
    }

    public static Optional<String> loadTexturesProperty(SkeBossPlugin plugin, String skinFile, String profileName) {
        Optional<File> file = resolveSkinFile(plugin, skinFile);
        if (file.isEmpty()) {
            return Optional.empty();
        }
        try {
            byte[] png = Files.readAllBytes(file.get().toPath());
            return Optional.of(buildTexturesProperty(png, profileName));
        } catch (IOException ex) {
            plugin.getLogger().warning("스킨 파일 읽기 실패: " + file.get().getPath() + " — " + ex.getMessage());
            return Optional.empty();
        }
    }

    public static String buildTexturesProperty(byte[] pngBytes, String profileName) {
        String safeName = profileName == null || profileName.isBlank() ? "SkeBossMinion" : profileName;
        String skinBase64 = Base64.getEncoder().encodeToString(pngBytes);
        String innerJson = "{"
                + "\"timestamp\":" + System.currentTimeMillis() + ","
                + "\"profileId\":\"" + UUID.randomUUID() + "\","
                + "\"profileName\":\"" + escapeJson(safeName) + "\","
                + "\"textures\":{\"SKIN\":{\"url\":\"data:image/png;base64," + skinBase64 + "\"}}"
                + "}";
        return Base64.getEncoder().encodeToString(innerJson.getBytes(StandardCharsets.UTF_8));
    }

    public static UUID profileUuidFor(String key) {
        return UUID.nameUUIDFromBytes(("skeboss-minion:" + key).getBytes(StandardCharsets.UTF_8));
    }

    private static String escapeJson(String value) {
        return value.replace("\\", "\\\\").replace("\"", "\\\"");
    }
}
