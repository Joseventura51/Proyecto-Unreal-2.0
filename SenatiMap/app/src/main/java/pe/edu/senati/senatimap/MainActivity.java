package pe.edu.senati.senatimap;

import android.Manifest;
import android.annotation.SuppressLint;
import android.app.Activity;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothManager;
import android.bluetooth.le.BluetoothLeScanner;
import android.bluetooth.le.ScanCallback;
import android.bluetooth.le.ScanRecord;
import android.bluetooth.le.ScanResult;
import android.bluetooth.le.ScanSettings;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.ParcelUuid;
import android.provider.Settings;
import android.util.SparseArray;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

public class MainActivity extends Activity {
    private WebView webView;
    private static final int REQ_LOCATION = 41;
    private static final int REQ_BLE = 42;
    private static final ParcelUuid EDDYSTONE_UUID =
        ParcelUuid.fromString("0000feaa-0000-1000-8000-00805f9b34fb");

    private BluetoothLeScanner bleScanner;
    private ScanCallback scanCallback;
    private boolean scanning = false;

    private LocationManager locationManager;
    private LocationListener liveLocationListener;
    private boolean liveLocationEnabled = false;
    private boolean locationUpdatesActive = false;
    private final Handler handler = new Handler(Looper.getMainLooper());
    private final Map<String, Double> filteredRssi = new HashMap<>();
    private final Runnable autoStopScan = () -> stopBeaconScanInternal("Escaneo completado");

    @SuppressLint({"SetJavaScriptEnabled", "AddJavascriptInterface"})
    @Override
    public void onCreate(Bundle state) {
        super.onCreate(state);

        webView = new WebView(this);
        setContentView(webView);

        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(true);

        webView.setWebViewClient(new WebViewClient());
        webView.setWebChromeClient(new WebChromeClient());
        webView.addJavascriptInterface(new Bridge(), "SenatiAndroid");
        webView.loadUrl("file:///android_asset/index.html");
    }

    public class Bridge {
        @JavascriptInterface
        public void requestLocation() {
            runOnUiThread(() -> requestLocationPermission());
        }

        @JavascriptInterface
        public void startBeaconScan() {
            runOnUiThread(() -> requestBlePermissionAndScan());
        }

        @JavascriptInterface
        public void stopBeaconScan() {
            runOnUiThread(() -> stopBeaconScanInternal("Escaneo detenido"));
        }

        @JavascriptInterface
        public void openBluetoothSettings() {
            runOnUiThread(() -> startActivity(new Intent(Settings.ACTION_BLUETOOTH_SETTINGS)));
        }
    }

    private void requestLocationPermission() {
        if (checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED &&
            checkSelfPermission(Manifest.permission.ACCESS_COARSE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
            requestPermissions(new String[]{
                Manifest.permission.ACCESS_FINE_LOCATION,
                Manifest.permission.ACCESS_COARSE_LOCATION
            }, REQ_LOCATION);
            return;
        }
        liveLocationEnabled = true;
        startLiveLocation();
    }

    @SuppressLint("MissingPermission")
    private void startLiveLocation() {
        if (!liveLocationEnabled) return;
        if (checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED &&
            checkSelfPermission(Manifest.permission.ACCESS_COARSE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
            return;
        }

        stopLocationUpdates();
        locationManager = (LocationManager) getSystemService(LOCATION_SERVICE);

        try {
            Location best = null;
            Location gps = locationManager.getLastKnownLocation(LocationManager.GPS_PROVIDER);
            Location network = locationManager.getLastKnownLocation(LocationManager.NETWORK_PROVIDER);
            best = gps;
            if (network != null && (best == null || network.getTime() > best.getTime())) best = network;
            if (best != null) pushLocation(best);

            liveLocationListener = new LocationListener() {
                @Override
                public void onLocationChanged(Location location) {
                    if (location != null) pushLocation(location);
                }

                @Override
                public void onProviderEnabled(String provider) {
                    emitLocationState(true, "Seguimiento en vivo activo");
                }

                @Override
                public void onProviderDisabled(String provider) {
                    // Puede quedar otro proveedor activo, no detenemos el seguimiento completo.
                }
            };

            boolean providerStarted = false;

            if (locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER)) {
                locationManager.requestLocationUpdates(
                    LocationManager.GPS_PROVIDER,
                    1200L,
                    0.8f,
                    liveLocationListener,
                    Looper.getMainLooper()
                );
                providerStarted = true;
            }

            if (locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)) {
                locationManager.requestLocationUpdates(
                    LocationManager.NETWORK_PROVIDER,
                    2200L,
                    1.5f,
                    liveLocationListener,
                    Looper.getMainLooper()
                );
                providerStarted = true;
            }

            locationUpdatesActive = providerStarted;
            if (providerStarted) {
                emitLocationState(true, "Seguimiento en vivo activo");
            } else {
                emitLocationState(false, "Activa la ubicación del teléfono");
                Toast.makeText(this, "Activa la ubicación del teléfono.", Toast.LENGTH_SHORT).show();
            }
        } catch (Exception error) {
            locationUpdatesActive = false;
            emitLocationState(false, "No se pudo iniciar el seguimiento");
            Toast.makeText(this, "No se pudo iniciar la ubicación en vivo.", Toast.LENGTH_SHORT).show();
        }
    }

    @SuppressLint("MissingPermission")
    private void stopLocationUpdates() {
        if (locationManager != null && liveLocationListener != null) {
            try { locationManager.removeUpdates(liveLocationListener); } catch (Exception ignored) {}
        }
        locationUpdatesActive = false;
    }

    private void emitLocationState(boolean active, String message) {
        String safe = JSONObject.quote(message);
        String js = "SenatiMap.locationTrackingState(" + active + "," + safe + ")";
        webView.post(() -> webView.evaluateJavascript(js, null));
    }

    private void pushLocation(Location location) {
        String js = String.format(Locale.US,
            "SenatiMap.setUserLocation(%f,%f,%f,false)",
            location.getLatitude(), location.getLongitude(), location.getAccuracy());
        webView.post(() -> webView.evaluateJavascript(js, null));
    }

    private boolean hasBlePermissions() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            return checkSelfPermission(Manifest.permission.BLUETOOTH_SCAN) == PackageManager.PERMISSION_GRANTED &&
                   checkSelfPermission(Manifest.permission.BLUETOOTH_CONNECT) == PackageManager.PERMISSION_GRANTED;
        }
        return checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED;
    }

    private void requestBlePermissionAndScan() {
        if (!hasBlePermissions()) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                requestPermissions(new String[]{
                    Manifest.permission.BLUETOOTH_SCAN,
                    Manifest.permission.BLUETOOTH_CONNECT
                }, REQ_BLE);
            } else {
                requestPermissions(new String[]{Manifest.permission.ACCESS_FINE_LOCATION}, REQ_BLE);
            }
            return;
        }
        startBeaconScanInternal();
    }

    @SuppressLint("MissingPermission")
    private void startBeaconScanInternal() {
        BluetoothManager bluetoothManager = (BluetoothManager) getSystemService(BLUETOOTH_SERVICE);
        BluetoothAdapter adapter = bluetoothManager == null ? null : bluetoothManager.getAdapter();

        if (adapter == null) {
            emitScanState(false, "Este teléfono no tiene Bluetooth.");
            return;
        }
        if (!adapter.isEnabled()) {
            emitScanState(false, "Bluetooth está apagado.");
            return;
        }

        if (scanning) stopBeaconScanInternal("Reiniciando escaneo");
        filteredRssi.clear();

        bleScanner = adapter.getBluetoothLeScanner();
        if (bleScanner == null) {
            emitScanState(false, "No se pudo iniciar el escáner BLE.");
            return;
        }

        scanCallback = new ScanCallback() {
            @Override
            public void onScanResult(int callbackType, ScanResult result) {
                emitBeacon(result);
            }

            @Override
            public void onBatchScanResults(List<ScanResult> results) {
                for (ScanResult result : results) emitBeacon(result);
            }

            @Override
            public void onScanFailed(int errorCode) {
                scanning = false;
                emitScanState(false, "Error BLE: " + errorCode);
            }
        };

        ScanSettings scanSettings = new ScanSettings.Builder()
            .setScanMode(ScanSettings.SCAN_MODE_LOW_LATENCY)
            .build();

        bleScanner.startScan(null, scanSettings, scanCallback);
        scanning = true;
        emitScanState(true, "Buscando anuncios BLE durante 15 segundos…");
        handler.removeCallbacks(autoStopScan);
        handler.postDelayed(autoStopScan, 15000);
    }

    @SuppressLint("MissingPermission")
    private void stopBeaconScanInternal(String message) {
        handler.removeCallbacks(autoStopScan);
        if (scanning && bleScanner != null && scanCallback != null) {
            try { bleScanner.stopScan(scanCallback); } catch (Exception ignored) {}
        }
        scanning = false;
        emitScanState(false, message);
    }

    private double smoothRssi(String key, int raw) {
        Double old = filteredRssi.get(key);
        double filtered = old == null ? raw : (old * 0.72) + (raw * 0.28);
        filteredRssi.put(key, filtered);
        return filtered;
    }

    private boolean validCalibration(double value) {
        return value >= -110 && value <= -20;
    }

    private Double estimateDistance(double measuredAtOneMeter, double rssi) {
        if (!validCalibration(measuredAtOneMeter) || rssi < -100) return null;
        final double pathLossExponent = 2.15; // entorno interior aproximado
        double distance = Math.pow(10.0, (measuredAtOneMeter - rssi) / (10.0 * pathLossExponent));
        if (!Double.isFinite(distance) || distance < 0.03 || distance > 80.0) return null;
        return distance;
    }

    private String formatUuid(byte[] data, int offset) {
        if (data == null || data.length < offset + 16) return "";
        String hex = toHexRange(data, offset, 16);
        if (hex.length() < 32) return hex;
        return hex.substring(0, 8) + "-" +
               hex.substring(8, 12) + "-" +
               hex.substring(12, 16) + "-" +
               hex.substring(16, 20) + "-" +
               hex.substring(20, 32);
    }

    private String toHexRange(byte[] bytes, int offset, int length) {
        if (bytes == null) return "";
        StringBuilder value = new StringBuilder();
        int limit = Math.min(bytes.length, offset + length);
        for (int i = offset; i < limit; i++) value.append(String.format(Locale.US, "%02X", bytes[i]));
        return value.toString();
    }

    @SuppressLint("MissingPermission")
    private void emitBeacon(ScanResult result) {
        try {
            ScanRecord record = result.getScanRecord();
            String name = null;
            if (record != null) name = record.getDeviceName();
            if ((name == null || name.trim().isEmpty()) && result.getDevice() != null) {
                try { name = result.getDevice().getName(); } catch (Exception ignored) {}
            }
            if (name == null || name.trim().isEmpty()) name = "Dispositivo BLE sin nombre";

            String address = result.getDevice() != null ? result.getDevice().getAddress() : "Sin MAC";
            int rawRssi = result.getRssi();
            double rssi = smoothRssi(address, rawRssi);

            String protocol = "BLE";
            boolean isBeacon = false;
            Double measuredAtOneMeter = null;
            String beaconId = "";

            // iBeacon: manufacturer Apple 0x004C, prefijo 0x02 0x15.
            if (record != null) {
                byte[] apple = record.getManufacturerSpecificData(0x004C);
                if (apple != null && apple.length >= 23 &&
                    (apple[0] & 0xFF) == 0x02 && (apple[1] & 0xFF) == 0x15) {
                    protocol = "iBeacon";
                    isBeacon = true;
                    int measuredPower = apple[22]; // byte firmado: RSSI esperado a 1 m
                    if (validCalibration(measuredPower)) measuredAtOneMeter = (double) measuredPower;

                    int major = ((apple[18] & 0xFF) << 8) | (apple[19] & 0xFF);
                    int minor = ((apple[20] & 0xFF) << 8) | (apple[21] & 0xFF);
                    beaconId = formatUuid(apple, 2) + " · " + major + "/" + minor;
                }
            }

            // Eddystone: Service UUID FEAA. Su ranging data es potencia calibrada a 0 m.
            if (record != null && !isBeacon) {
                byte[] eddystone = record.getServiceData(EDDYSTONE_UUID);
                if (eddystone != null && eddystone.length >= 2) {
                    int frameType = eddystone[0] & 0xFF;
                    protocol = frameType == 0x00 ? "Eddystone UID" :
                               frameType == 0x10 ? "Eddystone URL" :
                               frameType == 0x20 ? "Eddystone TLM" :
                               frameType == 0x30 ? "Eddystone EID" : "Eddystone";
                    isBeacon = true;

                    if (frameType == 0x00 || frameType == 0x10 || frameType == 0x30) {
                        int calibratedAtZero = eddystone[1]; // byte firmado
                        double oneMeter = calibratedAtZero - 41.0;
                        if (validCalibration(oneMeter)) measuredAtOneMeter = oneMeter;
                    }
                    if (frameType == 0x00 && eddystone.length >= 18) {
                        beaconId = toHexRange(eddystone, 2, 16);
                    }
                }
            }

            Double distance = measuredAtOneMeter == null ? null : estimateDistance(measuredAtOneMeter, rssi);

            int advertisedRadioTx = Integer.MIN_VALUE;
            if (record != null) advertisedRadioTx = record.getTxPowerLevel();

            JSONArray services = new JSONArray();
            if (record != null) {
                List<ParcelUuid> uuids = record.getServiceUuids();
                if (uuids != null) {
                    for (ParcelUuid uuid : uuids) services.put(uuid.toString());
                }
            }

            JSONArray manufacturers = new JSONArray();
            if (record != null) {
                SparseArray<byte[]> data = record.getManufacturerSpecificData();
                for (int i = 0; i < data.size(); i++) {
                    JSONObject manufacturer = new JSONObject();
                    manufacturer.put("id", data.keyAt(i));
                    manufacturer.put("hex", toHex(data.valueAt(i), 24));
                    manufacturers.put(manufacturer);
                }
            }

            JSONObject payload = new JSONObject();
            payload.put("name", name);
            payload.put("address", address);
            payload.put("rssi", Math.round(rssi));
            payload.put("rawRssi", rawRssi);
            payload.put("protocol", protocol);
            payload.put("isBeacon", isBeacon);
            payload.put("calibrated", measuredAtOneMeter != null);
            payload.put("calibrationPower", measuredAtOneMeter == null ? JSONObject.NULL : Math.round(measuredAtOneMeter));
            payload.put("radioTxPower", advertisedRadioTx == Integer.MIN_VALUE ? JSONObject.NULL : advertisedRadioTx);
            payload.put("distance", distance == null ? JSONObject.NULL : distance);
            payload.put("beaconId", beaconId);
            payload.put("services", services);
            payload.put("manufacturers", manufacturers);
            payload.put("timestamp", System.currentTimeMillis());

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                payload.put("connectable", result.isConnectable());
            }

            String js = "SenatiMap.onBeacon(" + payload.toString() + ")";
            webView.post(() -> webView.evaluateJavascript(js, null));
        } catch (Exception ignored) {}
    }

    private String toHex(byte[] bytes, int maxBytes) {
        if (bytes == null) return "";
        StringBuilder value = new StringBuilder();
        int limit = Math.min(bytes.length, maxBytes);
        for (int i = 0; i < limit; i++) value.append(String.format(Locale.US, "%02X", bytes[i]));
        if (bytes.length > maxBytes) value.append("…");
        return value.toString();
    }

    private void emitScanState(boolean active, String message) {
        String safe = JSONObject.quote(message);
        String js = "SenatiMap.beaconScanState(" + active + "," + safe + ")";
        webView.post(() -> webView.evaluateJavascript(js, null));
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] results) {
        super.onRequestPermissionsResult(requestCode, permissions, results);

        if (requestCode == REQ_LOCATION) {
            for (int result : results) {
                if (result == PackageManager.PERMISSION_GRANTED) {
                    liveLocationEnabled = true;
                    startLiveLocation();
                    return;
                }
            }
            Toast.makeText(this, "Permiso de ubicación denegado.", Toast.LENGTH_SHORT).show();
        }

        if (requestCode == REQ_BLE) {
            if (hasBlePermissions()) {
                startBeaconScanInternal();
            } else {
                emitScanState(false, "Permisos Bluetooth denegados.");
            }
        }
    }

    @Override
    protected void onPause() {
        stopLocationUpdates();
        super.onPause();
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (liveLocationEnabled) startLiveLocation();
    }

    @Override
    protected void onDestroy() {
        stopLocationUpdates();
        stopBeaconScanInternal("Escaneo detenido");
        super.onDestroy();
    }

    @SuppressWarnings("deprecation")
    @Override
    public void onBackPressed() {
        if (webView.canGoBack()) webView.goBack();
        else super.onBackPressed();
    }
}
