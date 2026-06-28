package com.skeboss.command;

import com.skeboss.SkeBossPlugin;
import com.skeboss.boss.BossManager;
import com.skeboss.boss.SkeBoss;
import com.skeboss.boss.SkillDefinition;
import com.skeboss.minion.MinionManager;
import com.skeboss.minion.MinionSpawner;
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

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public final class SkeBossCommand implements CommandExecutor, TabCompleter {

    private final BossManager bossManager;
    private final MinionManager minionManager;
    private final WeaponManager weaponManager;

    public SkeBossCommand(BossManager bossManager, MinionManager minionManager, WeaponManager weaponManager) {
        this.bossManager = bossManager;
        this.minionManager = minionManager;
        this.weaponManager = weaponManager;
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
        sender.sendMessage(TextUtil.color("&aconfig.yml 리로드 완료. &7(이미 스폰된 보스·잡몹은 재시작 권장)"));
        sender.sendMessage(TextUtil.color("&7무기: &f" + String.join(", ", weaponManager.getWeaponConfig().getWeaponIds())));
    }

    private void handleMinion(Player player, String[] args) {
        if (args.length >= 2 && args[1].equalsIgnoreCase("check")) {
            handleMinionCheck(player);
            return;
        }

        if (args.length < 2) {
            player.sendMessage(TextUtil.color("&c/skeboss minion <check|spawner> ..."));
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

    private void handleMinionCheck(Player player) {
        var config = minionManager.getConfig();
        var me = minionManager.getModelEngine();

        player.sendMessage(TextUtil.color("&6&l━━━━ 잡몹 진단 ━━━━"));
        player.sendMessage(TextUtil.color("&7스킨 닉네임: &f" + config.getSkinUsername()));
        player.sendMessage(TextUtil.color("&7config model-id: &f" + config.getModelId()));

        String resolved = me.resolveFirstAvailableModelId(config.getModelId(), config.getModelFallbackIds());
        if (me.hasBlueprint(resolved)) {
            player.sendMessage(TextUtil.color("&a사용할 모델: &f" + resolved + " &a(있음)"));
        } else {
            player.sendMessage(TextUtil.color("&c사용할 모델: &f" + resolved + " &c(없음!)"));
            player.sendMessage(TextUtil.color("&e→ plugins/ModelEngine/blueprints/ 에 &fplayer_model.bbmodel"));
            player.sendMessage(TextUtil.color("&e→ 넣고 &f/meg reload models &e후 config model-id: player_model"));
        }

        player.sendMessage(TextUtil.color("&7폴백 목록:"));
        for (String id : config.getModelFallbackIds()) {
            String status = me.hasBlueprint(id) ? "&a있음" : "&c없음";
            player.sendMessage(TextUtil.color("  &f" + id + " " + status));
        }

        if (me.hasBlueprint("ske")) {
            player.sendMessage(TextUtil.color("&7인조인간 모델(ske): &a있음 &7— 리소스팩은 보스 기준으로 적용됨"));
        }

        player.sendMessage(TextUtil.color("&7player limb 없으면 &c좀비만&7 보입니다. 스킨 PNG만으로는 안 됩니다."));
        player.sendMessage(TextUtil.color("&7완전 투명하면 &f/skeboss minion check &7로 blueprint 확인 후 &f/meg reload models"));
        player.sendMessage(TextUtil.color("&7위키: &fhttps://git.mythiccraft.io/mythiccraft/model-engine-4/-/wikis/Modeling/Bone-Behaviors"));
        player.sendMessage(TextUtil.color("&7→ player_model.bbmodel 링크 저장 → blueprints 폴더"));
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
            sender.sendMessage(TextUtil.color("&e/skeboss minion spawner create <ID> &7- 잡몹 스포너 생성"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion spawner remove <ID> &7- 잡몹 스포너 삭제"));
            sender.sendMessage(TextUtil.color("&e/skeboss minion spawner list &7- 스포너 목록"));
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
            return filter(Bukkit.getWorlds().stream().map(World::getName).toList(), args[1]);
        }
        if (args.length == 2 && args[0].equalsIgnoreCase("minion")
                && sender.hasPermission("skeboss.admin")) {
            return filter(List.of("check", "spawner"), args[1]);
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
