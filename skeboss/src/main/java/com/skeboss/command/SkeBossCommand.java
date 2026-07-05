package com.skeboss.command;

import com.skeboss.SkeBossPlugin;
import com.skeboss.blueprint.ModelBlueprintPaths;
import com.skeboss.boss.BossConfig;
import com.skeboss.boss.BossManager;
import com.skeboss.boss.SkeBoss;
import com.skeboss.boss.SkillDefinition;
import com.skeboss.minion.MinionBlueprintInstaller;
import com.skeboss.minion.MinionManager;
import com.skeboss.minion.MinionSpawner;
import com.skeboss.modelengine.ModelEngineBridge;
import com.skeboss.coin.CoinManager;
import com.skeboss.cutscene.CutsceneDefinition;
import com.skeboss.cutscene.CutsceneManager;
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
    private final CoinManager coinManager;
    private final CutsceneManager cutsceneManager;

    public SkeBossCommand(BossManager bossManager, MinionManager minionManager,
                          WeaponManager weaponManager, CoinManager coinManager,
                          CutsceneManager cutsceneManager) {
        this.bossManager = bossManager;
        this.minionManager = minionManager;
        this.weaponManager = weaponManager;
        this.coinManager = coinManager;
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
            case "boss" -> {
                return requireAdmin(sender, () -> handleBoss((Player) sender, args));
            }
            case "coin" -> {
                return handleCoin(sender, args);
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
            target.sendMessage(TextUtil.color("&a인조인간의 코어를 받았습니다."));
        }
        return true;
    }

    private boolean handleSpawnCommand(CommandSender sender, String[] args) {
        if (!sender.hasPermission("skeboss.admin")) {
            sender.sendMessage(TextUtil.color("&c권한이 없습니다. &7/skeboss help"));
            return true;
        }

        String presetId = resolvePresetArg(args);
        Location location = resolveSpawnLocation(sender, args, presetId);
        if (location == null) {
            return true;
        }

        try {
            SkeBoss boss = presetId != null
                    ? bossManager.spawn(presetId, location)
                    : bossManager.spawn(location);
            String pos = String.format("%.1f, %.1f, %.1f", location.getX(), location.getY(), location.getZ());
            String presetLabel = boss.getConfig().getDisplayName();
            sender.sendMessage(TextUtil.color(
                    "&a보스 스폰: &f" + presetLabel
                            + " &7(" + boss.getConfig().getPresetId() + ")"
                            + " &7" + location.getWorld().getName()
                            + " &f(" + pos + ")"
            ));
        } catch (IllegalArgumentException | IllegalStateException ex) {
            sender.sendMessage(TextUtil.color("&c스폰 실패: &f" + ex.getMessage()));
            SkeBossPlugin.getInstance().getLogger().severe("보스 스폰 실패: " + ex.getMessage());
            if (ex.getCause() != null) {
                ex.getCause().printStackTrace();
            }
        }
        return true;
    }

    private String resolvePresetArg(String[] args) {
        if (args.length < 2) {
            return null;
        }
        String token = args[1].toLowerCase(Locale.ROOT);
        if (isNumericSpawnArg(token)) {
            return null;
        }
        if (bossManager.getPresetRegistry().getPresetIds().contains(token)
                || SkeBossPlugin.getInstance().getConfig().isConfigurationSection("boss-presets." + token)) {
            return token;
        }
        return null;
    }

    private boolean isNumericSpawnArg(String token) {
        try {
            Double.parseDouble(token);
            return true;
        } catch (NumberFormatException ex) {
            return false;
        }
    }

    private Location resolveSpawnLocation(CommandSender sender, String[] args, String presetId) {
        int base = presetId != null ? 2 : 1;

        if (args.length <= base) {
            if (sender instanceof Player player) {
                return player.getLocation();
            }
            sender.sendMessage(TextUtil.color("&c콘솔: &f/skeboss spawn [프리셋] <world> <x> <y> <z>"));
            return null;
        }

        if (args.length == base + 3) {
            if (!(sender instanceof Player player)) {
                sender.sendMessage(TextUtil.color("&c콘솔: &f/skeboss spawn [프리셋] <world> <x> <y> <z>"));
                return null;
            }
            return parseSpawnLocation(sender, player.getWorld(), args[base], args[base + 1], args[base + 2]);
        }

        if (args.length == base + 4) {
            World world = Bukkit.getWorld(args[base]);
            if (world == null) {
                sender.sendMessage(TextUtil.color("&c월드를 찾을 수 없습니다: &f" + args[base]));
                return null;
            }
            return parseSpawnLocation(sender, world, args[base + 1], args[base + 2], args[base + 3]);
        }

        sender.sendMessage(TextUtil.color("&c/skeboss spawn [프리셋] [x y z]"));
        sender.sendMessage(TextUtil.color("&c/skeboss spawn [프리셋] <world> <x> <y> <z>"));
        sender.sendMessage(TextUtil.color("&7프리셋: &f" + String.join(", ", bossManager.getPresetRegistry().getPresetIds())));
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

        SkillDefinition skill = boss.getConfig().getSkills().stream()
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

    private boolean handleCoin(CommandSender sender, String[] args) {
        if (!sender.hasPermission("skeboss.weapon.give") && !sender.hasPermission("skeboss.admin")) {
            sender.sendMessage(TextUtil.color("&c권한이 없습니다. &7/skeboss coin"));
            return true;
        }

        Player target;
        if (args.length >= 2) {
            target = Bukkit.getPlayerExact(args[1]);
            if (target == null) {
                sender.sendMessage(TextUtil.color("&c플레이어를 찾을 수 없습니다: &f" + args[1]));
                return true;
            }
        } else if (sender instanceof Player player) {
            target = player;
        } else {
            sender.sendMessage(TextUtil.color("&c콘솔: &f/skeboss coin <플레이어>"));
            return true;
        }

        int amount = 1;
        if (args.length >= 3) {
            try {
                amount = Math.max(1, Integer.parseInt(args[2]));
            } catch (NumberFormatException ex) {
                sender.sendMessage(TextUtil.color("&c개수는 숫자로 입력하세요."));
                return true;
            }
        }

        ItemStack coin = coinManager.createCoinItem(amount);
        var leftover = target.getInventory().addItem(coin);
        if (!leftover.isEmpty()) {
            leftover.values().forEach(stack -> target.getWorld().dropItemNaturally(target.getLocation(), stack));
            sender.sendMessage(TextUtil.color("&e인벤토리 가득 참 — 바닥에 드롭했습니다."));
        }
        sender.sendMessage(TextUtil.color("&a코인 지급: &f" + amount + "개 &7→ &f" + target.getName()));
        if (!sender.equals(target)) {
            target.sendMessage(TextUtil.color("&a코인을 받았습니다. &7우클릭으로 던지세요."));
        }
        return true;
    }

    private void handleReload(CommandSender sender) {
        SkeBossPlugin plugin = com.skeboss.SkeBossPlugin.getInstance();
        plugin.mergeAndReloadConfig();
        weaponManager.reload();
        coinManager.reload();
        bossManager.reload();
        minionManager.reload();
        cutsceneManager.reload();
        sender.sendMessage(TextUtil.color("&aconfig.yml 리로드 완료. &7(이미 스폰된 보스·잡몹은 재시작 권장)"));
        sender.sendMessage(TextUtil.color("&7보스 프리셋: &f" + String.join(", ", bossManager.getPresetRegistry().getPresetIds())));
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
            Player target = resolveCutsceneTarget(sender, args, 2);
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
            Player target = resolveCutsceneTarget(sender, args, 2);
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

    private Player resolveCutsceneTarget(CommandSender sender, String[] args, int idIndex) {
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

    private void handleBoss(Player player, String[] args) {
        if (args.length >= 2 && args[1].equalsIgnoreCase("check")) {
            String presetId = args.length >= 3 ? args[2] : "fire-swordsman";
            handleBossCheck(player, presetId);
            return;
        }
        player.sendMessage(TextUtil.color("&c/skeboss boss check [프리셋]"));
    }

    private void handleBossCheck(Player player, String presetId) {
        SkeBossPlugin plugin = SkeBossPlugin.getInstance();
        var me = minionManager.getModelEngine();
        BossConfig config;
        try {
            config = bossManager.getPresetRegistry().load(plugin, presetId);
        } catch (IllegalArgumentException ex) {
            player.sendMessage(TextUtil.color("&c알 수 없는 프리셋: &f" + presetId));
            player.sendMessage(TextUtil.color("&7사용 가능: &f" + String.join(", ", bossManager.getPresetRegistry().getPresetIds())));
            return;
        }

        String modelId = config.getModelId();
        Path blueprintFile = ModelBlueprintPaths.blueprintPath(plugin, modelId);

        player.sendMessage(TextUtil.color("&6&l━━━━ 보스 모델 진단 ━━━━"));
        player.sendMessage(TextUtil.color("&7프리셋: &f" + presetId));
        player.sendMessage(TextUtil.color("&7표시 이름: &f" + config.getDisplayName()));
        player.sendMessage(TextUtil.color("&7config model-id: &f" + modelId));
        if (config.usesPlayerSkin()) {
            player.sendMessage(TextUtil.color("&7스킨 닉네임: &f" + config.getSkinUsername()
                    + " &7(PlayerLimb — 잡몹과 동일 방식)"));
        }

        if (config.usesPlayerSkin()) {
            Path playerModel = ModelBlueprintPaths.blueprintPath(plugin, "player_model");
            if (Files.isRegularFile(playerModel)) {
                player.sendMessage(TextUtil.color("&aPlayerLimb: &fplayer_model.bbmodel &a(있음)"));
            } else {
                player.sendMessage(TextUtil.color("&cPlayerLimb: &fplayer_model.bbmodel &c없음"));
                player.sendMessage(TextUtil.color("&e→ &f/skeboss minion install-model &e한 번 실행"));
            }
            String resolved = me.resolveAvailableModelId(modelId, config.getModelFallbackIds());
            if (resolved != null) {
                player.sendMessage(TextUtil.color("&aModelEngine 등록: &f" + resolved + " &a(있음)"));
            } else {
                player.sendMessage(TextUtil.color("&cModelEngine 등록: &f없음 &c→ /meg reload (전체)"));
            }
            player.sendMessage(TextUtil.color("&7── 설정 순서 (플레이어 스킨) ──"));
            player.sendMessage(TextUtil.color("  &e1. &f/skeboss minion install-model &e(ir_hand 검 본 포함)"));
            player.sendMessage(TextUtil.color("  &e2. &f/meg reload &e(전체) + 리소스팩 재수락"));
            player.sendMessage(TextUtil.color("  &e3. 스킨 닉네임 &f" + config.getSkinUsername() + " &e확인 (Mojang)"));
            player.sendMessage(TextUtil.color("  &e4. &f/skeboss spawn " + presetId));
            player.sendMessage(TextUtil.color("&7CHUNSAMGOD는 &cbbmodel 이름이 아니라 스킨 닉네임&7입니다"));
            return;
        }

        if (Files.isRegularFile(blueprintFile)) {
            player.sendMessage(TextUtil.color("&a파일: &f" + blueprintFile.getFileName() + " &a(폴더에 있음)"));
            player.sendMessage(TextUtil.color("&7경로: &8" + blueprintFile));
        } else {
            player.sendMessage(TextUtil.color("&c파일: &f" + modelId + ".bbmodel &c없음"));
            player.sendMessage(TextUtil.color("&7필요 경로: &8" + blueprintFile));
            if ("CHUNSAMGOD".equalsIgnoreCase(modelId)) {
                player.sendMessage(TextUtil.color("&e→ CHUNSAMGOD.bbmodel 을 직접 넣어야 합니다 (플러그인 JAR에 없음)"));
            } else if ("ske".equalsIgnoreCase(modelId)) {
                player.sendMessage(TextUtil.color("&e→ ske.bbmodel 을 blueprints 폴더에 복사하세요"));
                player.sendMessage(TextUtil.color("&7  (레포: &fskeboss/reference/ske.bbmodel&7)"));
            }
        }

        if (me.hasBlueprint(modelId)) {
            player.sendMessage(TextUtil.color("&aModelEngine 등록: &f" + modelId + " &a(있음)"));
        } else {
            player.sendMessage(TextUtil.color("&cModelEngine 등록: &f없음"));
            if (!Files.isRegularFile(blueprintFile)) {
                player.sendMessage(TextUtil.color("&7원인: &cbbmodel 파일이 없어서 /meg reload 해도 등록 안 됨"));
            } else {
                player.sendMessage(TextUtil.color("&7원인: &c/meg reload &7(전체) 후 리소스팩 재수락 필요"));
                player.sendMessage(TextUtil.color("&7  bbmodel 내부 &fmodel_identifier&7 가 &f" + modelId + "&7 인지 Blockbench에서 확인"));
            }
        }

        player.sendMessage(TextUtil.color("&7── 설정 순서 ──"));
        player.sendMessage(TextUtil.color("  &e1. &fplugins/ModelEngine/blueprints/" + modelId + ".bbmodel &e배치"));
        player.sendMessage(TextUtil.color("  &e2. &f/meg reload &e(&cmodels만 말고 전체 reload&7)"));
        player.sendMessage(TextUtil.color("  &e3. 클라이언트 리소스팩 &c다시 받기"));
        player.sendMessage(TextUtil.color("  &e4. &f/skeboss spawn " + presetId));
        player.sendMessage(TextUtil.color("&7모델 없어도 boss-v17+ 스킬은 동작 (좀비만 보임)"));
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

        if (args.length < 2) {
            player.sendMessage(TextUtil.color("&c/skeboss minion <check|skin-test|install-model|spawner> ..."));
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
                    player.sendMessage(TextUtil.color("&c/skeboss minion spawner create <ID>"));
                    player.sendMessage(TextUtil.color("&7현재 위치에 EMP4348 잡몹 스포너를 만듭니다."));
                    return;
                }
                String id = args[3];
                try {
                    MinionSpawner spawner = minionManager.createSpawner(id, player.getLocation());
                    String pos = String.format("%.1f, %.1f, %.1f",
                            player.getLocation().getX(),
                            player.getLocation().getY(),
                            player.getLocation().getZ());
                    player.sendMessage(TextUtil.color(
                            "&a잡몹 스포너 생성: &f" + spawner.getId()
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
                    player.sendMessage(TextUtil.color("&f" + spawner.getId() + " &7" + loc.getWorld().getName()
                            + " (" + pos + ") " + active));
                }
            }
            default -> player.sendMessage(TextUtil.color("&c/skeboss minion spawner <create|remove|list> ..."));
        }
    }

    private void handleMinionSkinTest(Player player, String[] args) {
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
            sender.sendMessage(TextUtil.color("&e/skeweapon &7- 인조인간의 코어 바로 지급"));
            sender.sendMessage(TextUtil.color("&e/skeboss weapon <이름> [플레이어] &7- 무기 지급"));
            sender.sendMessage(TextUtil.color("&e/skeboss coin [플레이어] [개수] &7- 코인 지급"));
            sender.sendMessage(TextUtil.color("&7  무기: &f" + String.join(", ", weaponManager.getWeaponConfig().getWeaponIds())));
        }
        if (sender.hasPermission("skeboss.weapon.use") && sender instanceof Player) {
            sender.sendMessage(TextUtil.color("&e/skeboss cast <laser|chain> &7- 스킬 직접 시전"));
            sender.sendMessage(TextUtil.color("&7  무기 우클릭으로도 사용 가능 (OP 불필요)"));
        }
        if (sender.hasPermission("skeboss.admin")) {
            sender.sendMessage(TextUtil.color("&e/skeboss spawn [프리셋] &7- 보스 스폰 (기본: default)"));
            sender.sendMessage(TextUtil.color("&e/skeboss spawn fire-swordsman &7- 불의 검사 (CHUNSAMGOD 스킨)"));
            sender.sendMessage(TextUtil.color("&7  프리셋: &f" + String.join(", ", bossManager.getPresetRegistry().getPresetIds())));
            sender.sendMessage(TextUtil.color("&e/skeboss boss check [프리셋] &7- 보스 모델·ME 등록 진단"));
            sender.sendMessage(TextUtil.color("&e/skeboss skill [이름] &7- 보스 스킬 테스트"));
            sender.sendMessage(TextUtil.color("&e/skeboss remove &7- 보스 제거"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion check &7- 잡몹 모델·스킨 진단"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion skin-test [닉네임] &7- 스킨 조회 테스트"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion install-model &7- player_model.bbmodel 설치"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion spawner create <ID> &7- 잡몹 스포너 생성"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion spawner remove <ID> &7- 잡몹 스포너 삭제"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion spawner list &7- 스포너 목록"));
            sender.sendMessage(TextUtil.color("&e/skeboss reload &7- 설정 리로드"));
        }
        if (sender.hasPermission("skeboss.cutscene.play") || sender.hasPermission("skeboss.admin")) {
            sender.sendMessage(TextUtil.color("&e/skeboss cutscene play <ID> &7- 스토리 연출"));
            sender.sendMessage(TextUtil.color("&7  컷신: &f" + String.join(", ", cutsceneManager.getScenes().keySet())));
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
                options.addAll(List.of("spawn", "skill", "remove", "reload", "minion", "boss"));
            }
            if (sender.hasPermission("skeboss.weapon.give") || sender.hasPermission("skeboss.admin")) {
                options.add("weapon");
                options.add("coin");
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
        if (args.length == 2 && args[0].equalsIgnoreCase("skill") && sender.hasPermission("skeboss.admin")) {
            List<String> names = bossManager.getConfig().getSkills().stream().map(SkillDefinition::id).toList();
            return filter(names, args[1]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("weapon")
                && (sender.hasPermission("skeboss.weapon.give") || sender.hasPermission("skeboss.admin"))) {
            return filter(weaponManager.getWeaponConfig().getWeaponIds(), args[1]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("spawn") && sender.hasPermission("skeboss.admin")) {
            List<String> options = new ArrayList<>(bossManager.getPresetRegistry().getPresetIds());
            options.addAll(Bukkit.getWorlds().stream().map(World::getName).toList());
            return filter(options, args[1]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("boss")
                && sender.hasPermission("skeboss.admin")) {
            return filter(List.of("check"), args[1]);
        }
        if (args.length == 3 && args[0].equalsIgnoreCase("boss")
                && args[1].equalsIgnoreCase("check") && sender.hasPermission("skeboss.admin")) {
            return filter(bossManager.getPresetRegistry().getPresetIds(), args[2]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("cutscene")) {
            return filter(List.of("list", "play", "stop"), args[1]);
        }
        if (args.length == 3 && args[0].equalsIgnoreCase("cutscene") && args[1].equalsIgnoreCase("play")) {
            return filter(new ArrayList<>(cutsceneManager.getScenes().keySet()), args[2]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("minion")
                && sender.hasPermission("skeboss.admin")) {
            return filter(List.of("check", "skin-test", "skintest", "install-model", "install", "spawner"), args[1]);
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
