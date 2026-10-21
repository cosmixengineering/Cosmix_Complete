package com.cosmix.mobile

import android.app.Application
import com.facebook.react.PackageList
import com.facebook.react.ReactApplication
import com.facebook.react.ReactHost
import com.facebook.react.ReactNativeApplicationEntryPoint.loadReactNative
import com.facebook.react.defaults.DefaultReactHost.getDefaultReactHost

class MainApplication : Application(), ReactApplication {

  override val reactHost: ReactHost by lazy {
    getDefaultReactHost(
      context = applicationContext,
      packageList =
        PackageList(this).packages.apply {
          // Packages that cannot be autolinked yet can be added manually here, for example:
          // add(MyReactNativePackage())
        },
    )
  }

  override fun onCreate() {
    super.onCreate()
    if (BuildConfig.DEBUG) {
      // adb reverse keeps Metro on loopback for both emulators and USB devices.
      getSharedPreferences("${packageName}_preferences", MODE_PRIVATE).edit()
          .putString("debug_http_host", "localhost:8081").apply()
    }
    loadReactNative(this)
  }
}
