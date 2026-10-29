package com.cosmix.mobile

import com.facebook.react.BaseReactPackage
import com.facebook.react.bridge.NativeModule
import com.facebook.react.bridge.ReactApplicationContext
import com.facebook.react.bridge.ReactContextBaseJavaModule
import com.facebook.react.module.model.ReactModuleInfo
import com.facebook.react.module.model.ReactModuleInfoProvider

/** Public configuration from the same native build that carries the pinned network policy. */
class ReleaseConfigurationModule(context: ReactApplicationContext) : ReactContextBaseJavaModule(context) {
  override fun getName() = "CosmixReleaseConfiguration"
  override fun getConstants(): Map<String, Any> = mapOf("apiBaseUrl" to BuildConfig.COSMIX_API_BASE_URL)
}

class ReleaseConfigurationPackage : BaseReactPackage() {
  override fun getModule(name: String, reactContext: ReactApplicationContext): NativeModule? =
    if (name == "CosmixReleaseConfiguration") ReleaseConfigurationModule(reactContext) else null
  override fun getReactModuleInfoProvider() = ReactModuleInfoProvider {
    mapOf("CosmixReleaseConfiguration" to ReactModuleInfo(
      "CosmixReleaseConfiguration", ReleaseConfigurationModule::class.java.name, false, false, false, false
    ))
  }
}
