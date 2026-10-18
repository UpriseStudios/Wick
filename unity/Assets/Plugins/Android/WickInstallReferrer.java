package com.uprisestudios.wick;

import android.content.Context;

import com.android.installreferrer.api.InstallReferrerClient;
import com.android.installreferrer.api.InstallReferrerStateListener;
import com.android.installreferrer.api.ReferrerDetails;

/**
 * Reads the Play install referrer once ("vault=CODE" when the app was installed from a vault
 * link's store button). Unity polls result(): null while waiting, "" if there is none.
 */
public final class WickInstallReferrer {
    private static volatile String result;

    public static void start(Context context) {
        if (result != null) {
            return;
        }

        final InstallReferrerClient client = InstallReferrerClient.newBuilder(context).build();
        try {
            client.startConnection(new InstallReferrerStateListener() {
                @Override
                public void onInstallReferrerSetupFinished(int code) {
                    String referrer = "";
                    if (code == InstallReferrerClient.InstallReferrerResponse.OK) {
                        try {
                            ReferrerDetails details = client.getInstallReferrer();
                            referrer = details.getInstallReferrer();
                        } catch (Exception e) {
                            referrer = "";
                        }
                    }

                    result = referrer == null ? "" : referrer;
                    client.endConnection();
                }

                @Override
                public void onInstallReferrerServiceDisconnected() {
                    if (result == null) {
                        result = "";
                    }
                }
            });
        } catch (Exception e) {
            result = "";
        }
    }

    public static String result() {
        return result;
    }
}
