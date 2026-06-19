using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace IdleRPG
{
    public enum LoginPanelMode
    {
        Login,
        Register
    }

    [Serializable]
    public sealed class StoredAccount
    {
        public string accountId = string.Empty;
        public string username = string.Empty;
        public string passwordHash = string.Empty;
        public string salt = string.Empty;
        public bool isGuest;
        public long createdUnixSeconds;
    }

    [Serializable]
    public sealed class AccountRegistry
    {
        public List<StoredAccount> accounts = new List<StoredAccount>();
    }

    [Serializable]
    public sealed class SessionData
    {
        public string accountId = string.Empty;
        public string username = string.Empty;
        public bool isGuest;
    }

    public static class IdleRpgAccountService
    {
        public const string LegacySaveKey = "idle-rpg-save-v1";
        public const string SaveKeyPrefix = "idle-rpg-save-v1-";
        public const string SessionKey = "idle-rpg-session";
        public const string AccountsKey = "idle-rpg-accounts";

        public const int MinUsernameLength = 2;
        public const int MaxUsernameLength = 12;
        public const int MinPasswordLength = 4;
        public const int MaxPasswordLength = 24;

        public static bool TryGetSession(out SessionData session)
        {
            session = null;
            string serialized = PlayerPrefs.GetString(SessionKey, string.Empty);
            if (string.IsNullOrEmpty(serialized))
            {
                return false;
            }

            try
            {
                session = JsonUtility.FromJson<SessionData>(serialized);
                if (session == null || string.IsNullOrEmpty(session.accountId))
                {
                    return false;
                }

                return FindAccount(session.accountId) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void SetSession(SessionData session)
        {
            if (session == null || string.IsNullOrEmpty(session.accountId))
            {
                return;
            }

            PlayerPrefs.SetString(SessionKey, JsonUtility.ToJson(session));
            PlayerPrefs.Save();
        }

        public static void ClearSession()
        {
            PlayerPrefs.DeleteKey(SessionKey);
            PlayerPrefs.Save();
        }

        public static string GetSaveKey(string accountId)
        {
            return SaveKeyPrefix + accountId;
        }

        public static bool TryLogin(string username, string password, out SessionData session, out string errorMessage)
        {
            session = null;
            errorMessage = string.Empty;

            if (!ValidateUsername(username, out errorMessage))
            {
                return false;
            }

            if (string.IsNullOrEmpty(password))
            {
                errorMessage = "비밀번호를 입력하세요.";
                return false;
            }

            StoredAccount account = FindAccountByUsername(NormalizeUsername(username));
            if (account == null)
            {
                errorMessage = "존재하지 않는 계정입니다.";
                return false;
            }

            if (account.isGuest)
            {
                errorMessage = "게스트 계정은 비밀번호 로그인을 사용할 수 없습니다.";
                return false;
            }

            if (!VerifyPassword(password, account.passwordHash, account.salt))
            {
                errorMessage = "비밀번호가 일치하지 않습니다.";
                return false;
            }

            session = CreateSession(account);
            SetSession(session);
            return true;
        }

        public static bool TryRegister(string username, string password, string passwordConfirm, out SessionData session, out string errorMessage)
        {
            session = null;
            errorMessage = string.Empty;

            if (!ValidateUsername(username, out errorMessage))
            {
                return false;
            }

            if (!ValidatePassword(password, out errorMessage))
            {
                return false;
            }

            if (password != passwordConfirm)
            {
                errorMessage = "비밀번호 확인이 일치하지 않습니다.";
                return false;
            }

            string normalizedUsername = NormalizeUsername(username);
            if (FindAccountByUsername(normalizedUsername) != null)
            {
                errorMessage = "이미 사용 중인 닉네임입니다.";
                return false;
            }

            string salt = GenerateSalt();
            StoredAccount account = new StoredAccount
            {
                accountId = Guid.NewGuid().ToString("N"),
                username = normalizedUsername,
                passwordHash = HashPassword(password, salt),
                salt = salt,
                isGuest = false,
                createdUnixSeconds = NowUnixSeconds()
            };

            AccountRegistry registry = LoadRegistry();
            registry.accounts.Add(account);
            SaveRegistry(registry);

            session = CreateSession(account);
            SetSession(session);
            return true;
        }

        public static bool TryCreateGuest(out SessionData session, out string errorMessage)
        {
            session = null;
            errorMessage = string.Empty;

            string guestName = CreateGuestUsername();
            StoredAccount account = new StoredAccount
            {
                accountId = Guid.NewGuid().ToString("N"),
                username = guestName,
                passwordHash = string.Empty,
                salt = string.Empty,
                isGuest = true,
                createdUnixSeconds = NowUnixSeconds()
            };

            AccountRegistry registry = LoadRegistry();
            registry.accounts.Add(account);
            SaveRegistry(registry);
            MigrateLegacySaveToAccount(account.accountId);

            session = CreateSession(account);
            SetSession(session);
            return true;
        }

        public static bool ResumeSessionAccount(out SessionData session)
        {
            if (!TryGetSession(out session))
            {
                return false;
            }

            MigrateLegacySaveToAccount(session.accountId);
            return true;
        }

        public static void MigrateLegacySaveToAccount(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return;
            }

            string legacyData = PlayerPrefs.GetString(LegacySaveKey, string.Empty);
            if (string.IsNullOrEmpty(legacyData))
            {
                return;
            }

            string accountSaveKey = GetSaveKey(accountId);
            if (!string.IsNullOrEmpty(PlayerPrefs.GetString(accountSaveKey, string.Empty)))
            {
                PlayerPrefs.DeleteKey(LegacySaveKey);
                PlayerPrefs.Save();
                return;
            }

            PlayerPrefs.SetString(accountSaveKey, legacyData);
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();
        }

        public static void DeleteAccountSave(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
            {
                return;
            }

            PlayerPrefs.DeleteKey(GetSaveKey(accountId));
            PlayerPrefs.Save();
        }

        public static void ResetAllAccountData()
        {
            AccountRegistry registry = LoadRegistry();
            for (int index = 0; index < registry.accounts.Count; index += 1)
            {
                DeleteAccountSave(registry.accounts[index].accountId);
            }

            PlayerPrefs.DeleteKey(AccountsKey);
            PlayerPrefs.DeleteKey(SessionKey);
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();
        }

        private static SessionData CreateSession(StoredAccount account)
        {
            return new SessionData
            {
                accountId = account.accountId,
                username = account.username,
                isGuest = account.isGuest
            };
        }

        private static AccountRegistry LoadRegistry()
        {
            string serialized = PlayerPrefs.GetString(AccountsKey, string.Empty);
            if (string.IsNullOrEmpty(serialized))
            {
                return new AccountRegistry();
            }

            try
            {
                AccountRegistry registry = JsonUtility.FromJson<AccountRegistry>(serialized);
                if (registry == null || registry.accounts == null)
                {
                    return new AccountRegistry();
                }

                return registry;
            }
            catch (Exception)
            {
                return new AccountRegistry();
            }
        }

        private static void SaveRegistry(AccountRegistry registry)
        {
            PlayerPrefs.SetString(AccountsKey, JsonUtility.ToJson(registry));
            PlayerPrefs.Save();
        }

        private static StoredAccount FindAccount(string accountId)
        {
            AccountRegistry registry = LoadRegistry();
            for (int index = 0; index < registry.accounts.Count; index += 1)
            {
                if (registry.accounts[index].accountId == accountId)
                {
                    return registry.accounts[index];
                }
            }

            return null;
        }

        private static StoredAccount FindAccountByUsername(string username)
        {
            AccountRegistry registry = LoadRegistry();
            for (int index = 0; index < registry.accounts.Count; index += 1)
            {
                if (registry.accounts[index].username == username)
                {
                    return registry.accounts[index];
                }
            }

            return null;
        }

        private static string CreateGuestUsername()
        {
            string suffix = UnityEngine.Random.Range(1000, 9999).ToString();
            return "게스트" + suffix;
        }

        private static bool ValidateUsername(string username, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(username))
            {
                errorMessage = "닉네임을 입력하세요.";
                return false;
            }

            string trimmed = username.Trim();
            if (trimmed.Length < MinUsernameLength || trimmed.Length > MaxUsernameLength)
            {
                errorMessage = "닉네임은 " + MinUsernameLength + "~" + MaxUsernameLength + "자여야 합니다.";
                return false;
            }

            for (int index = 0; index < trimmed.Length; index += 1)
            {
                char character = trimmed[index];
                if (char.IsLetterOrDigit(character) || character == '_' || character >= '\uAC00')
                {
                    continue;
                }

                errorMessage = "닉네임은 한글, 영문, 숫자, _ 만 사용할 수 있습니다.";
                return false;
            }

            return true;
        }

        private static bool ValidatePassword(string password, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrEmpty(password))
            {
                errorMessage = "비밀번호를 입력하세요.";
                return false;
            }

            if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
            {
                errorMessage = "비밀번호는 " + MinPasswordLength + "~" + MaxPasswordLength + "자여야 합니다.";
                return false;
            }

            return true;
        }

        private static string NormalizeUsername(string username)
        {
            return username.Trim();
        }

        private static string GenerateSalt()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string HashPassword(string password, string salt)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + ":" + password));
                StringBuilder builder = new StringBuilder(bytes.Length * 2);
                for (int index = 0; index < bytes.Length; index += 1)
                {
                    builder.Append(bytes[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static bool VerifyPassword(string password, string expectedHash, string salt)
        {
            return HashPassword(password, salt) == expectedHash;
        }

        private static long NowUnixSeconds()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
