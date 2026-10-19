package com.uprisestudios.wick;

import android.app.Activity;
import android.content.SharedPreferences;
import android.preference.PreferenceManager;

import com.google.android.ump.ConsentDebugSettings;
import com.google.android.ump.ConsentInformation;
import com.google.android.ump.ConsentRequestParameters;
import com.google.android.ump.UserMessagingPlatform;

/**
 * Google's User Messaging Platform consent form (GDPR / UK GDPR), shown when the player's region
 * needs it. The message itself is configured in AdMob (Privacy & messaging), and the manifest needs
 * the AdMob app id. Unity polls state(): 0 waiting, 1 done, 2 failed. personalised() reads the IAB
 * TCF purposes the form stored (1: store/access, 3 and 4: personalised ads profile and selection).
 */
public final class WickConsent {
    private static volatile int state;
    private static volatile String error = "";

    public static void run(final Activity activity, final boolean underAge, final String testDeviceHash) {
        state = 0;
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    ConsentRequestParameters.Builder params = new ConsentRequestParameters.Builder()
                        .setTagForUnderAgeOfConsent(underAge);
                    if (testDeviceHash != null && !testDeviceHash.isEmpty()) {
                        params.setConsentDebugSettings(new ConsentDebugSettings.Builder(activity)
                            .setDebugGeography(ConsentDebugSettings.DebugGeography.DEBUG_GEOGRAPHY_EEA)
                            .addTestDeviceHashedId(testDeviceHash)
                            .build());
                    }

                    final ConsentInformation info = UserMessagingPlatform.getConsentInformation(activity);
                    info.requestConsentInfoUpdate(activity, params.build(),
                        () -> UserMessagingPlatform.loadAndShowConsentFormIfRequired(activity, formError -> {
                            if (formError != null) {
                                error = formError.getMessage();
                            }
                            state = 1;
                        }),
                        requestError -> {
                            error = requestError.getMessage();
                            state = 2;
                        });
                } catch (Throwable t) {
                    error = String.valueOf(t.getMessage());
                    state = 2;
                }
            }
        });
    }

    public static int state() {
        return state;
    }

    public static String error() {
        return error;
    }

    /** True if the player may be shown ads at all (consent obtained or not required). */
    public static boolean canRequestAds(Activity activity) {
        return UserMessagingPlatform.getConsentInformation(activity).canRequestAds();
    }

    /** Whether the stored TCF consent allows personalised ads (purposes 1, 3 and 4). */
    public static boolean personalised(Activity activity) {
        SharedPreferences prefs = PreferenceManager.getDefaultSharedPreferences(activity);
        String purposes = prefs.getString("IABTCF_PurposeConsents", "");
        if (prefs.getInt("IABTCF_gdprApplies", 0) == 0) {
            return true; // outside GDPR regions there's no TCF consent to require
        }

        return purposes.length() >= 4 && purposes.charAt(0) == '1' && purposes.charAt(2) == '1' && purposes.charAt(3) == '1';
    }
}
