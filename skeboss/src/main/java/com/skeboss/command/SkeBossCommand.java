package com.skeboss.command;

import com.skeboss.SkeBossPlugin;
import com.skeboss.boss.BossManager;
import com.skeboss.boss.SkeBoss;
import com.skeboss.boss.SkillDefinition;
import com.skeboss.cutscene.CutsceneDefinition;
import com.skeboss.cutscene.CutsceneManager;
import com.skeboss.minion.MinionBlueprintInstaller;
import com.skeboss.minion.MinionManager;
import com.skeboss.minion.MinionSpawner;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.util.TextUtil;
import com.skeboss.weapon.WeaponManager;
import org.bukkit.Bukkit;
import org.bukkit.Location;
import org.bukkit.World;
import org.bukkit.command.Command;
import org.bukkit.command.CommandExecutor;
import org.bukkit.command.CommandSender;
import org.bukkit.command.TabCompleter;
import org.bukkit.entity.LivingEntity;
import org.bukkit.entity.Player;
import org.bukkit.inventory.ItemStack;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.UUID;

public final class SkeBossCommand implements CommandExecutor, TabCompleter {

    private final BossManager bossManager;
    private final MinionManager minionManager;
    private final WeaponManager weaponManager;
    private final CutsceneManager cutsceneManager;

    public SkeBossCommand(BossManager bossManager, MinionManager minionManager, WeaponManager weaponManager,
                          CutsceneManager cutsceneManager) {
        this.bossManager = bossManager;
        this.minionManager = minionManager;
        this.weaponManager = weaponManager;
        this.cutsceneManager = cutsceneManager;
    }

    @Override
    public boolean onCommand(CommandSender sender, Command command, String label, String[] args) {
        if (command.getName().equalsIgnoreCase("skeweapon")
                || command.getName().equalsIgnoreCase("인조무기")
                || command.getName().equalsIgnoreCase("skebossweapon")) {
            return handleQuickWeapon(sender, args);
        }

        if (args.length == 0 || args[0].equalsIgnoreCase("help")) {
            sendHelp(sender);
            return true;
        }

        String sub = args[0].toLowerCase(Locale.ROOT);
        switch (sub) {
            case "cast" -> {
                return handleCast(sender, args);
            }
            case "weapon" -> {
                return handleWeapon(sender, args);
            }
            case "spawn" -> {
                return handleSpawnCommand(sender, args);
            }
            case "remove", "kill" -> {
                return requireAdmin(sender, () -> handleRemove((Player) sender));
            }
            case "skill" -> {
                return requireAdmin(sender, () -> handleSkill((Player) sender, args));
            }
            case "reload" -> {
                return requireAdmin(sender, () -> handleReload(sender));
            }
            case "minion" -> {
                return requireAdmin(sender, () -> handleMinion((Player) sender, args));
            }
            case "cutscene", "scene", "연출" -> {
                return handleCutscene(sender, args);
            }
            default -> {
                sender.sendMessage(TextUtil.color("&c알 수 없는 명령입니다. &7/skeboss help"));
                return true;
            }
        }
    }

    private boolean requireAdmin(CommandSender sender, Runnable action) {
        if (!(sender instanceof Player player)) {
            sender.sendMessage("플레이어만 사용할 수 있습니다.");
            return true;
        }
        if (!player.hasPermission("skeboss.admin")) {
            player.sendMessage(TextUtil.color("&c권한이 없습니다. &7/skeboss help"));
            return true;
        }
        action.run();
        return true;
    }

    private boolean handleCast(CommandSender sender, String[] args) {
        if (!(sender instanceof Player player)) {
            sender.sendMessage("플레이어만 사용할 수 있습니다.");
            return true;
        }
        if (!player.hasPermission("skeboss.weapon.use")) {
            player.sendMessage(TextUtil.color("&c무기 사용 권한이 없습니다."));
            return true;
        }
        if (args.length < 2) {
            player.sendMessage(TextUtil.color("&c/skeboss cast <laser|chain>"));
            return true;
        }
        weaponManager.castSkill(player, args[1]);
        return true;
    }

    private boolean canGiveWeapon(CommandSender sender) {
        return sender.hasPermission("skeboss.weapon.give") || sender.hasPermission("skeboss.admin");
    }

    /** /skeweapon — artificial-arm 바로 지급 */
    private boolean handleQuickWeapon(CommandSender sender, String[] args) {
        if (!canGiveWeapon(sender)) {
            sender.sendMessage(TextUtil.color("&c권한이 없습니다. &7(OP 또는 skeboss.weapon.give)"));
            return true;
        }

        Player target;
        if (args.length >= 1) {
            target = Bukkit.getPlayer(args[0]);
            if (target == null) {
                sender.sendMessage(TextUtil.color("&c플레이어를 찾을 수 없습니다: &f" + args[0]));
                return true;
            }
        } else if (sender instanceof Player player) {
            target = player;
        } else {
            sender.sendMessage(TextUtil.color("&c콘솔: &f/skeweapon <플레이어>"));
            return true;
        }

        return giveWeapon(sender, target, "artificial-arm");
    }

    private boolean handleWeapon(CommandSender sender, String[] args) {
        if (!canGiveWeapon(sender)) {
            sender.sendMessage(TextUtil.color("&c권한이 없습니다. &7(OP 또는 skeboss.weapon.give)"));
            return true;
        }
        if (args.length < 2) {
            sender.sendMessage(TextUtil.color("&c/skeboss weapon <이름> [플레이어]"));
            sender.sendMessage(TextUtil.color("&7무기 목록: &f" + String.join(", ", weaponManager.getWeaponConfig().getWeaponIds())));
            sender.sendMessage(TextUtil.color("&7빠른 지급: &f/skeweapon"));
            return true;
        }

        String weaponId = args[1];
        Player target;
        if (args.length >= 3) {
            target = Bukkit.getPlayer(args[2]);
            if (target == null) {
                sender.sendMessage(TextUtil.color("&c플레이어를 찾을 수 없습니다."));
                return true;
            }
        } else if (sender instanceof Player player) {
            target = player;
        } else {
            sender.sendMessage(TextUtil.color("&c콘솔에서는 플레이어를 지정하세요."));
            return true;
        }

        return giveWeapon(sender, target, weaponId);
    }

    private boolean giveWeapon(CommandSender sender, Player target, String weaponId) {
        ItemStack weapon = weaponManager.createWeapon(weaponId);
        if (weapon == null) {
            sender.sendMessage(TextUtil.color("&c무기 없음: &f" + weaponId));
            sender.sendMessage(TextUtil.color("&7목록: &f" + String.join(", ", weaponManager.getWeaponConfig().getWeaponIds())));
            sender.sendMessage(TextUtil.color("&7config에 weapons 섹션이 있는지 확인 후 &f/skeboss reload"));
            return true;
        }

        var leftover = target.getInventory().addItem(weapon);
        if (!leftover.isEmpty()) {
            for (ItemStack drop : leftover.values()) {
                target.getWorld().dropItemNaturally(target.getLocation(), drop);
            }
            sender.sendMessage(TextUtil.color("&e인벤토리 가득 참 — 바닥에 드롭했습니다."));
        }

        sender.sendMessage(TextUtil.color("&a무기 지급: &f" + weaponManager.getWeaponId(weapon) + " &7→ &f" + target.getName()));
        if (!target.equals(sender)) {
            target.sendMessage(TextUtil.color("&a인조 무기를 받았습니다."));
        }
        return true;
    }

    private boolean handleSpawnCommand(CommandSender sender, String[] args) {
        if (!sender.hasPermission("skeboss.admin")) {
            sender.sendMessage(TextUtil.color("&c권한이 없습니다. &7/skeboss help"));
            return true;
        }

        Location location = resolveSpawnLocation(sender, args);
        if (location == null) {
            return true;
        }

        try {
            SkeBoss boss = bossManager.spawn(location);
            String pos = String.format("%.1f, %.1f, %.1f", location.getX(), location.getY(), location.getZ());
            sender.sendMessage(TextUtil.color(
                    "&a인조인간 스폰 완료! &7" + location.getWorld().getName()
                            + " &f(" + pos + ") &7UUID: " + boss.getId()
            ));
        } catch (IllegalStateException ex) {
            sender.sendMessage(TextUtil.color("&c스폰 실패: &f" + ex.getMessage()));
            SkeBossPlugin.getInstance().getLogger().severe("보스 스폰 실패: " + ex.getMessage());
            if (ex.getCause() != null) {
                ex.getCause().printStackTrace();
            }
        }
        return true;
    }

    private Location resolveSpawnLocation(CommandSender sender, String[] args) {
        if (args.length == 1) {
            if (sender instanceof Player player) {
                return player.getLocation();
            }
            sender.sendMessage(TextUtil.color("&c콘솔: &f/skeboss spawn <world> <x> <y> <z>"));
            return null;
        }

        if (args.length == 4) {
            if (!(sender instanceof Player player)) {
                sender.sendMessage(TextUtil.color("&c콘솔: &f/skeboss spawn <world> <x> <y> <z>"));
                return null;
            }
            return parseSpawnLocation(sender, player.getWorld(), args[1], args[2], args[3]);
        }

        if (args.length == 5) {
            World world = Bukkit.getWorld(args[1]);
            if (world == null) {
                sender.sendMessage(TextUtil.color("&c월드를 찾을 수 없습니다: &f" + args[1]));
                return null;
            }
            return parseSpawnLocation(sender, world, args[2], args[3], args[4]);
        }

        sender.sendMessage(TextUtil.color("&c/skeboss spawn [x y z]"));
        sender.sendMessage(TextUtil.color("&c/skeboss spawn <world> <x> <y> <z>"));
        return null;
    }

    private Location parseSpawnLocation(CommandSender sender, World world, String xRaw, String yRaw, String zRaw) {
        try {
            double x = Double.parseDouble(xRaw);
            double y = Double.parseDouble(yRaw);
            double z = Double.parseDouble(zRaw);
            Location location = new Location(world, x, y, z);
            if (sender instanceof Player player) {
                location.setYaw(player.getLocation().getYaw());
                location.setPitch(player.getLocation().getPitch());
            }
            return location;
        } catch (NumberFormatException ex) {
            sender.sendMessage(TextUtil.color("&c좌표는 숫자로 입력하세요. 예: &f/skeboss spawn 100 64 -200"));
            return null;
        }
    }

    private void handleRemove(Player player) {
        SkeBoss boss = getTargetBoss(player);
        if (boss != null) {
            bossManager.remove(boss);
            player.sendMessage(TextUtil.color("&7보스 1마리 제거했습니다."));
            return;
        }
        int count = bossManager.getBosses().size();
        bossManager.removeAll();
        player.sendMessage(TextUtil.color("&7보스 " + count + "마리 제거했습니다."));
    }

    private void handleSkill(Player player, String[] args) {
        SkeBoss boss = getTargetBoss(player);
        if (boss == null) {
            player.sendMessage(TextUtil.color("&c바라보는 보스가 없습니다."));
            return;
        }

        SkillDefinition skill = bossManager.getConfig().getSkills().stream()
                .filter(def -> args.length < 2 || def.id().equalsIgnoreCase(args[1]))
                .findFirst()
                .orElse(null);

        if (skill == null) {
            player.sendMessage(TextUtil.color("&c스킬을 찾을 수 없습니다. /skeboss skill <이름>"));
            return;
        }

        boss.addAggro(player);
        boss.setTarget(player);
        bossManager.faceTarget(boss, player);

        if (bossManager.castSkill(boss, skill)) {
            player.sendMessage(TextUtil.color("&e스킬 &f" + skill.id() + " &e시전"));
        } else {
            player.sendMessage(TextUtil.color("&c스킬 사용 불가 (쿨타임 또는 시전 중)"));
        }
    }

    private void handleReload(CommandSender sender) {
        SkeBossPlugin plugin = com.skeboss.SkeBossPlugin.getInstance();
        plugin.mergeAndReloadConfig();
        weaponManager.reload();
        minionManager.reload();
        cutsceneManager.reload();
        sender.sendMessage(TextUtil.color("&aconfig.yml 리로드 완료. &7(이미 스폰된 보스·잡몹은 재시작 권장)"));
        sender.sendMessage(TextUtil.color("&7무기: &f" + String.join(", ", weaponManager.getWeaponConfig().getWeaponIds())));
        sender.sendMessage(TextUtil.color("&7컷신: &f" + String.join(", ", cutsceneManager.getScenes().keySet())));
    }

    private boolean handleCutscene(CommandSender sender, String[] args) {
        if (args.length < 2) {
            sender.sendMessage(TextUtil.color("&e/skeboss cutscene list"));
            sender.sendMessage(TextUtil.color("&e/skeboss cutscene play <ID> [플레이어]"));
            sender.sendMessage(TextUtil.color("&e/skeboss cutscene stop [플레이어]"));
            return true;
        }

        String action = args[1].toLowerCase(Locale.ROOT);
        if (action.equals("list")) {
            if (cutsceneManager.getScenes().isEmpty()) {
                sender.sendMessage(TextUtil.color("&7등록된 컷신이 없습니다. &fplugins/SkeBoss/cutscenes/"));
                return true;
            }
            sender.sendMessage(TextUtil.color("&6&l━━━━ 스토리 연출 목록 ━━━━"));
            for (CutsceneDefinition scene : cutsceneManager.getScenes().values()) {
                sender.sendMessage(TextUtil.color("&e" + scene.getId() + " &7— " + scene.getDescription()));
            }
            return true;
        }

        if (action.equals("play")) {
            if (args.length < 3) {
                sender.sendMessage(TextUtil.color("&c/skeboss cutscene play <ID> [플레이어]"));
                return true;
            }
            Player target = resolveTargetPlayer(sender, args, 2);
            if (target == null) {
                return true;
            }
            if (!sender.hasPermission("skeboss.cutscene.play") && !sender.hasPermission("skeboss.admin")) {
                sender.sendMessage(TextUtil.color("&c권한이 없습니다. &7skeboss.cutscene.play"));
                return true;
            }
            if (!cutsceneManager.play(target, args[2])) {
                sender.sendMessage(TextUtil.color("&c컷신을 찾을 수 없습니다: &f" + args[2]));
                return true;
            }
            if (!target.equals(sender)) {
                sender.sendMessage(TextUtil.color("&a" + target.getName() + " 에게 연출 재생: &f" + args[2]));
            }
            return true;
        }

        if (action.equals("stop")) {
            if (!sender.hasPermission("skeboss.admin")) {
                sender.sendMessage(TextUtil.color("&c권한이 없습니다."));
                return true;
            }
            Player target = resolveTargetPlayer(sender, args, 2);
            if (target == null) {
                return true;
            }
            cutsceneManager.stop(target, false);
            sender.sendMessage(TextUtil.color("&7연출 중단: &f" + target.getName()));
            return true;
        }

        sender.sendMessage(TextUtil.color("&c/skeboss cutscene <list|play|stop>"));
        return true;
    }

    private Player resolveTargetPlayer(CommandSender sender, String[] args, int idIndex) {
        if (args.length > idIndex + 1) {
            if (!sender.hasPermission("skeboss.admin")) {
                sender.sendMessage(TextUtil.color("&c다른 플레이어에게 재생하려면 OP가 필요합니다."));
                return null;
            }
            Player target = Bukkit.getPlayer(args[idIndex + 1]);
            if (target == null) {
                sender.sendMessage(TextUtil.color("&c플레이어를 찾을 수 없습니다: &f" + args[idIndex + 1]));
                return null;
            }
            return target;
        }
        if (!(sender instanceof Player player)) {
            sender.sendMessage(TextUtil.color("&c콘솔: 플레이어를 지정하세요."));
            return null;
        }
        return player;
    }

    private void handleMinion(Player player, String[] args) {
        if (args.length >= 2 && args[1].equalsIgnoreCase("check")) {
            handleMinionCheck(player);
            return;
        }

        if (args.length >= 2 && (args[1].equalsIgnoreCase("skin-test")
                || args[1].equalsIgnoreCase("skintest"))) {
            handleMinionSkinTest(player, args);
            return;
        }

        if (args.length >= 2 && (args[1].equalsIgnoreCase("install-model")
                || args[1].equalsIgnoreCase("install"))) {
            handleMinionInstallModel(player);
            return;
        }

        if (args.length >= 2 && args[1].equalsIgnoreCase("preset")
                && args.length >= 3 && args[2].equalsIgnoreCase("list")) {
            handleMinionPresetList(player);
            return;
        }

        if (args.length >= 2 && args[1].equalsIgnoreCase("spawn")) {
            handleMinionSpawn(player, args);
            return;
        }

        if (args.length < 2) {
            player.sendMessage(TextUtil.color("&c/skeboss minion <check|preset|spawn|skin-test|spawner> ..."));
            return;
        }

        String sub = args[1].toLowerCase(Locale.ROOT);
        if (!sub.equals("spawner")) {
            player.sendMessage(TextUtil.color("&c/skeboss minion spawner <create|remove|list> ..."));
            return;
        }

        if (args.length < 3) {
            player.sendMessage(TextUtil.color("&c/skeboss minion spawner <create|remove|list> ..."));
            return;
        }

        String action = args[2].toLowerCase(Locale.ROOT);
        switch (action) {
            case "create", "add" -> {
                if (args.length < 4) {
                    player.sendMessage(TextUtil.color("&c/skeboss minion spawner create <ID> [프리셋]"));
                    player.sendMessage(TextUtil.color("&7프리셋 생략 시 default-preset 사용"));
                    return;
                }
                String id = args[3];
                String presetId = args.length >= 5 ? args[4] : null;
                try {
                    MinionSpawner spawner = minionManager.createSpawner(id, player.getLocation(), presetId);
                    String pos = String.format("%.1f, %.1f, %.1f",
                            player.getLocation().getX(),
                            player.getLocation().getY(),
                            player.getLocation().getZ());
                    String presetLabel = spawner.getPresetId() != null
                            ? spawner.getPresetId()
                            : minionManager.getConfig().getDefaultPresetId();
                    player.sendMessage(TextUtil.color(
                            "&a잡몹 스포너 생성: &f" + spawner.getId()
                                    + " &7프리셋 &f" + presetLabel
                                    + " &7@ " + player.getWorld().getName() + " (" + pos + ")"
                    ));
                    player.sendMessage(TextUtil.color("&7죽으면 &f" + minionManager.getConfig().getSpawnerRespawnSeconds()
                            + "초&7 후 리스폰"));
                } catch (IllegalStateException ex) {
                    player.sendMessage(TextUtil.color("&c스포너 생성 실패: &f" + ex.getMessage()));
                }
            }
            case "remove", "delete", "del" -> {
                if (args.length < 4) {
                    player.sendMessage(TextUtil.color("&c/skeboss minion spawner remove <ID>"));
                    return;
                }
                String id = args[3];
                if (minionManager.removeSpawner(id)) {
                    player.sendMessage(TextUtil.color("&a잡몹 스포너 삭제: &f" + id));
                } else {
                    player.sendMessage(TextUtil.color("&c스포너를 찾을 수 없습니다: &f" + id));
                }
            }
            case "list" -> {
                var spawners = minionManager.getSpawnerStorage().all();
                if (spawners.isEmpty()) {
                    player.sendMessage(TextUtil.color("&7등록된 잡몹 스포너가 없습니다."));
                    return;
                }
                player.sendMessage(TextUtil.color("&6&l━━━━ 잡몹 스포너 ━━━━"));
                for (MinionSpawner spawner : spawners) {
                    Location loc = spawner.toLocation();
                    if (loc == null) {
                        player.sendMessage(TextUtil.color("&f" + spawner.getId() + " &c(월드 없음)"));
                        continue;
                    }
                    String pos = String.format("%.1f, %.1f, %.1f", loc.getX(), loc.getY(), loc.getZ());
                    String active = spawner.getActiveMinionId() != null ? "&a활성" : "&7대기";
                    String preset = spawner.getPresetId() != null
                            ? spawner.getPresetId()
                            : minionManager.getConfig().getDefaultPresetId();
                    player.sendMessage(TextUtil.color("&f" + spawner.getId() + " &7[" + preset + "] "
                            + loc.getWorld().getName()
                            + " (" + pos + ") " + active));
                }
            }
            default -> player.sendMessage(TextUtil.color("&c/skeboss minion spawner <create|remove|list> ..."));
        }
    }

    private void handleMinionPresetList(Player player) {
        var presets = minionManager.getConfig().getPresets();
        player.sendMessage(TextUtil.color("&6&l━━━━ 잡몹 프리셋 ━━━━"));
        for (var entry : presets.entrySet()) {
            var preset = entry.getValue();
            String skin = preset.hasSkinFile()
                    ? "PNG " + preset.getSkinFile()
                    : (preset.hasSkinUsername() ? "닉 " + preset.getSkinUsername() : "스킨 없음");
            player.sendMessage(TextUtil.color("&f" + entry.getKey() + " &7— "
                    + TextUtil.stripColor(preset.getDisplayName()) + " &8(" + skin + ")"));
        }
        player.sendMessage(TextUtil.color("&7PNG 경로: &fplugins/SkeBoss/skins/"));
    }

    private void handleMinionSpawn(Player player, String[] args) {
        if (args.length < 3) {
            player.sendMessage(TextUtil.color("&c/skeboss minion spawn <프리셋>"));
            return;
        }
        String presetId = args[2];
        try {
            minionManager.spawnPresetAt(player.getLocation(), presetId);
            player.sendMessage(TextUtil.color("&a잡몹 스폰: &f" + presetId));
        } catch (IllegalStateException ex) {
            player.sendMessage(TextUtil.color("&c스폰 실패: &f" + ex.getMessage()));
        }
    }

    private void handleMinionSkinTest(Player player, String[] args) {
        if (args.length >= 3 && args[2].equalsIgnoreCase("file")) {
            handleMinionSkinFileTest(player, args);
            return;
        }
        var config = minionManager.getConfig();
        String username = args.length >= 3 ? args[2] : config.getSkinUsername();
        var me = minionManager.getModelEngine();

        player.sendMessage(TextUtil.color("&6&l━━━━ 잡몹 스킨 테스트 ━━━━"));
        player.sendMessage(TextUtil.color("&7닉네임: &f" + username));

        Bukkit.getScheduler().runTaskAsynchronously(SkeBossPlugin.getInstance(), () -> {
            UUID asyncUuid = me.lookupUsernameUuid(username);
            String asyncTextures = asyncUuid != null ? me.lookupTexturesAsync(username) : null;
            final UUID uuid = asyncUuid;
            final String asyncResult = asyncTextures;
            Bukkit.getScheduler().runTask(SkeBossPlugin.getInstance(), () -> {
                player.sendMessage(TextUtil.color("&7비동기 UUID: "
                        + (uuid != null ? "&a" + uuid : "&c실패")));
                player.sendMessage(TextUtil.color("&7비동기 텍스처: "
                        + (asyncResult != null ? "&a" + asyncResult.length() + "자" : "&c실패")));

                ModelEngineBridge.SkinLookupResult result = me.testSkinLookup(username);
                if (result.success()) {
                    player.sendMessage(TextUtil.color("&a메인 스레드 조회 성공"));
                    player.sendMessage(TextUtil.color("&7UUID: &f" + result.uuid()));
                    player.sendMessage(TextUtil.color("&7텍스처: &f" + result.textures().length() + "자 (base64)"));
                } else {
                    player.sendMessage(TextUtil.color("&c메인 스레드 조회 실패: &f" + result.error()));
                }
            });
        });
    }

    private void handleMinionSkinFileTest(Player player, String[] args) {
        if (args.length < 4) {
            player.sendMessage(TextUtil.color("&c/skeboss minion skin-test file <파일명.png>"));
            player.sendMessage(TextUtil.color("&7예: guard.png → plugins/SkeBoss/skins/guard.png"));
            return;
        }
        var textures = com.skeboss.minion.SkinTexturesUtil.loadTexturesProperty(
                SkeBossPlugin.getInstance(), args[3], args[3]);
        player.sendMessage(TextUtil.color("&6&l━━━━ PNG 스킨 테스트 ━━━━"));
        if (textures.isPresent()) {
            player.sendMessage(TextUtil.color("&a파일 로드 성공 — textures " + textures.get().length() + "자"));
        } else {
            player.sendMessage(TextUtil.color("&c파일을 찾을 수 없거나 읽기 실패: &f" + args[3]));
        }
    }

    private void handleMinionInstallModel(Player player) {
        SkeBossPlugin plugin = SkeBossPlugin.getInstance();
        Path target = MinionBlueprintInstaller.blueprintPath(plugin);
        boolean existed = MinionBlueprintInstaller.isInstalled(plugin);
        try {
            MinionBlueprintInstaller.install(plugin);
        } catch (IOException ex) {
            player.sendMessage(TextUtil.color("&c모델 설치 실패: &f" + ex.getMessage()));
            return;
        }

        player.sendMessage(TextUtil.color("&6&l━━━━ 잡몹 모델 설치 ━━━━"));
        if (existed) {
            player.sendMessage(TextUtil.color("&e기존 파일 덮어씀: &f" + target));
        } else {
            player.sendMessage(TextUtil.color("&a복사 완료: &f" + target));
        }
        player.sendMessage(TextUtil.color("&e1. &f/skeboss minion install-model &e(덮어쓰기)"));
        player.sendMessage(TextUtil.color("&e2. &f/meg reload &e실행 (&cmodels만 말고 전체 reload&7 → 리소스팩 재생성)"));
        player.sendMessage(TextUtil.color("&e3. 클라이언트에서 리소스팩 &c다시 받기"));
        player.sendMessage(TextUtil.color("&e4. &f/skeboss minion check &e로 다시 확인"));
    }

    private void handleMinionCheck(Player player) {
        SkeBossPlugin plugin = SkeBossPlugin.getInstance();
        var config = minionManager.getConfig();
        var me = minionManager.getModelEngine();

        player.sendMessage(TextUtil.color("&6&l━━━━ 잡몹 진단 ━━━━"));
        player.sendMessage(TextUtil.color("&7스킨 닉네임: &f" + config.getSkinUsername()));
        player.sendMessage(TextUtil.color("&7config model-id: &f" + config.getModelId()));
        if (config.isBacklineMode()) {
            player.sendMessage(TextUtil.color("&7AI: &e후공몹 &7(맞기 전까지 공격 안 함)"));
        } else {
            player.sendMessage(TextUtil.color("&7AI: &c선공몹 &7(추격 &f" + config.getFollowRange() + "&7)"));
        }

        Path blueprintFile = MinionBlueprintInstaller.blueprintPath(plugin);
        if (Files.isRegularFile(blueprintFile)) {
            player.sendMessage(TextUtil.color("&a파일: &f" + blueprintFile.getFileName() + " &a(폴더에 있음)"));
        } else {
            player.sendMessage(TextUtil.color("&c파일: &fplayer_model.bbmodel &c없음"));
            player.sendMessage(TextUtil.color("&e→ &f/skeboss minion install-model &e한 번 실행하세요"));
        }

        String resolved = me.resolveAvailableModelId(config.getModelId(), config.getModelFallbackIds());
        if (resolved != null) {
            player.sendMessage(TextUtil.color("&aModelEngine 등록: &f" + resolved + " &a(있음)"));
        } else {
            player.sendMessage(TextUtil.color("&cModelEngine 등록: &f없음 &c(/meg reload models 필요)"));
        }

        player.sendMessage(TextUtil.color("&7폴백 목록 (&cskin/player는 기본 포함 아님&7):"));
        for (String id : config.getModelFallbackIds()) {
            String status = me.hasBlueprint(id) ? "&a있음" : "&c없음";
            player.sendMessage(TextUtil.color("  &f" + id + " " + status));
        }
        for (String extraId : List.of("skin_2")) {
            if (config.getModelFallbackIds().contains(extraId)) {
                continue;
            }
            String status = me.hasBlueprint(extraId) ? "&a있음" : "&c없음";
            player.sendMessage(TextUtil.color("  &f" + extraId + " " + status + " &7(bbmodel 내부 이름)"));
        }

        if (me.hasBlueprint("ske")) {
            player.sendMessage(TextUtil.color("&7인조인간 모델(ske): &a있음"));
        }

        player.sendMessage(TextUtil.color("&7EMP4348 스킨은 플러그인이 자동 적용. &cbbmodel 없으면 좀비만 보임."));
        player.sendMessage(TextUtil.color("&7로그에 PlayerLimb 6개 OK인데 머리만 보이면:"));
        player.sendMessage(TextUtil.color("  &e1. &f/meg reload &7(전체) 후 리소스팩 재수락"));
        player.sendMessage(TextUtil.color("  &e2. &finstall-model &7다시 실행 (ir_ 본 제거된 bbmodel)"));
        player.sendMessage(TextUtil.color("  &e3. &c셰이더 끄기&7 (Iris/모드 셰이더 = PlayerLimb 불가)"));
        player.sendMessage(TextUtil.color("&7콘솔에 &fsendToClient&7 로그가 있어야 클라이언트 반영됨"));
    }

    private SkeBoss getTargetBoss(Player player) {
        if (player.getTargetEntity(20) instanceof LivingEntity living && bossManager.isBoss(living)) {
            return bossManager.getBoss(living.getUniqueId());
        }
        return null;
    }

    private void sendHelp(CommandSender sender) {
        sender.sendMessage(TextUtil.color("&6&l━━━━ SkeBoss 명령어 ━━━━"));
        if (sender.hasPermission("skeboss.weapon.give") || sender.hasPermission("skeboss.admin")) {
            sender.sendMessage(TextUtil.color("&e/skeweapon &7- 인조 무기 바로 지급"));
            sender.sendMessage(TextUtil.color("&e/skeboss weapon <이름> [플레이어] &7- 무기 지급"));
            sender.sendMessage(TextUtil.color("&7  무기: &f" + String.join(", ", weaponManager.getWeaponConfig().getWeaponIds())));
        }
        if (sender.hasPermission("skeboss.weapon.use") && sender instanceof Player) {
            sender.sendMessage(TextUtil.color("&e/skeboss cast <laser|chain> &7- 스킬 직접 시전"));
            sender.sendMessage(TextUtil.color("&7  무기 우클릭으로도 사용 가능 (OP 불필요)"));
        }
        if (sender.hasPermission("skeboss.admin")) {
            sender.sendMessage(TextUtil.color("&e/skeboss spawn &7- 내 위치에 보스 스폰"));
            sender.sendMessage(TextUtil.color("&e/skeboss spawn <x> <y> <z> &7- 좌표에 스폰 (내 월드)"));
            sender.sendMessage(TextUtil.color("&e/skeboss spawn <world> <x> <y> <z> &7- 월드·좌표 지정"));
            sender.sendMessage(TextUtil.color("&e/skeboss skill [이름] &7- 보스 스킬 테스트"));
            sender.sendMessage(TextUtil.color("&e/skeboss remove &7- 보스 제거"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion check &7- 잡몹 모델·스킨 진단"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion skin-test [닉네임] &7- 스킨 조회 테스트"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion install-model &7- player_model.bbmodel 설치"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion preset list &7- 잡몹 프리셋 목록"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion spawn <프리셋> &7- 테스트 스폰"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion skin-test file <png> &7- PNG 스킨 테스트"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion spawner create <ID> [프리셋] &7- 잡몹 스포너"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion spawner remove <ID> &7- 잡몹 스포너 삭제"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion spawner list &7- 스포너 목록"));
            sender.sendMessage(TextUtil.color("&e/skeboss cutscene play <ID> [플레이어] &7- 스토리 연출 재생"));
            sender.sendMessage(TextUtil.color("&e/skeboss cutscene list &7- 연출 목록"));
            sender.sendMessage(TextUtil.color("&7  YAML: &fplugins/SkeBoss/cutscenes/"));
            sender.sendMessage(TextUtil.color("&e/skeboss reload &7- 설정 리로드"));
        }
        if (!sender.hasPermission("skeboss.weapon.give")
                && !sender.hasPermission("skeboss.admin")
                && !sender.hasPermission("skeboss.weapon.use")
                && !sender.hasPermission("skeboss.admin")) {
            sender.sendMessage(TextUtil.color("&7사용 가능한 명령이 없습니다."));
        }
    }

    @Override
    public List<String> onTabComplete(CommandSender sender, Command command, String alias, String[] args) {
        if (args.length == 1) {
            List<String> options = new ArrayList<>();
            if (sender.hasPermission("skeboss.admin")) {
                options.addAll(List.of("spawn", "skill", "remove", "reload", "minion"));
            }
            if (sender.hasPermission("skeboss.weapon.give") || sender.hasPermission("skeboss.admin")) {
                options.add("weapon");
            }
            if (sender.hasPermission("skeboss.weapon.use") && sender instanceof Player) {
                options.add("cast");
            }
            if (sender.hasPermission("skeboss.cutscene.play") || sender.hasPermission("skeboss.admin")) {
                options.add("cutscene");
            }
            options.add("help");
            return filter(options, args[0]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("cast") && sender.hasPermission("skeboss.weapon.use")) {
            return filter(List.of("laser", "chain"), args[1]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("cutscene")) {
            return filter(List.of("list", "play", "stop"), args[1]);
        }
        if (args.length == 3 && args[0].equalsIgnoreCase("cutscene") && args[1].equalsIgnoreCase("play")) {
            return filter(new ArrayList<>(cutsceneManager.getScenes().keySet()), args[2]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("skill") && sender.hasPermission("skeboss.admin")) {
            List<String> names = bossManager.getConfig().getSkills().stream().map(SkillDefinition::id).toList();
            return filter(names, args[1]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("weapon")
                && (sender.hasPermission("skeboss.weapon.give") || sender.hasPermission("skeboss.admin"))) {
            return filter(weaponManager.getWeaponConfig().getWeaponIds(), args[1]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("spawn") && sender.hasPermission("skeboss.admin")) {
            return filter(Bukkit.getWorlds().stream().map(World::getName).toList(), args[1]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("minion")
                && sender.hasPermission("skeboss.admin")) {
            return filter(List.of("check", "preset", "spawn", "skin-test", "skintest", "install-model", "install", "spawner"), args[1]);
        }
        if (args.length == 3 && args[0].equalsIgnoreCase("minion")
                && args[1].equalsIgnoreCase("spawn") && sender.hasPermission("skeboss.admin")) {
            return filter(new ArrayList<>(minionManager.getConfig().getPresets().keySet()), args[2]);
        }
        if (args.length == 3 && args[0].equalsIgnoreCase("minion")
                && args[1].equalsIgnoreCase("preset") && sender.hasPermission("skeboss.admin")) {
            return filter(List.of("list"), args[2]);
        }
        if (args.length == 3 && args[0].equalsIgnoreCase("minion")
                && args[1].equalsIgnoreCase("spawner") && sender.hasPermission("skeboss.admin")) {
            return filter(List.of("create", "remove", "list"), args[2]);
        }
        if (args.length == 4 && args[0].equalsIgnoreCase("minion")
                && args[1].equalsIgnoreCase("spawner") && args[2].equalsIgnoreCase("remove")
                && sender.hasPermission("skeboss.admin")) {
            return filter(minionManager.getSpawnerStorage().all().stream().map(MinionSpawner::getId).toList(), args[3]);
        }
        if (args.length == 5 && args[0].equalsIgnoreCase("minion")
                && args[1].equalsIgnoreCase("spawner") && args[2].equalsIgnoreCase("create")
                && sender.hasPermission("skeboss.admin")) {
            return filter(new ArrayList<>(minionManager.getConfig().getPresets().keySet()), args[4]);
        }
        return List.of();
    }

    private List<String> filter(List<String> options, String input) {
        String lower = input.toLowerCase(Locale.ROOT);
        List<String> result = new ArrayList<>();
        for (String option : options) {
            if (option.toLowerCase(Locale.ROOT).startsWith(lower)) {
                result.add(option);
            }
        }
        return result;
    }
}
