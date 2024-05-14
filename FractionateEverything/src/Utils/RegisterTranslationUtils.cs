using CommonAPI.Systems.ModLocalization;

namespace FractionateEverything.Utils {
    public static class RegisterTranslationUtils {
        public static void RegisterTranslation(string key, string enTrans, string cnTrans = null) {
            LocalizationModule.RegisterTranslation(key, enTrans, cnTrans ?? key, enTrans);
        }
    }
}
