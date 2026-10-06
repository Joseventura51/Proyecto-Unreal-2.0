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

import java.util.List;
import java.util.Locale;

public class MainActivity extends Activity {
    private WebView webView;
    private static final int REQ_LOCATION = 41;
    private static final int REQ_BLE = 42;

    private BluetoothLeScanner bleScanner;
    private ScanCallback scanCallback;
    private boolean scanning = false;
    private final Handler handler = new Handler(Looper.getMainLooper());
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
        locate();
    }

    @SuppressLint("MissingPermission")
    private void locate() {
        LocationManager manager = (LocationManager) getSystemService(LOCATION_SERVICE);
        Location best = null;

        try {
            Location gps = manager.getLastKnownLocation(LocationManager.GPS_PROVIDER);
            Location network = manager.getLastKnownLocation(LocationManager.NETWORK_PROVIDER);
            best = gps;
            if (network != null && (best == null || network.getTime() > best.getTime())) best = network;
            if (best != null) pushLocation(best);

            LocationListener listener = new LocationListener() {
                @Override
                public void onLocationChanged(Location location) {
                    pushLocation(location);
                    try { manager.removeUpdates(this); } catch (Exception ignored) {}
                }
            };

            if (manager.isProviderEnabled(LocationManager.GPS_PROVIDER)) {
                manager.requestSingleUpdate(LocationManager.GPS_PROVIDER, listener, Looper.getMainLooper());
            } else if (manager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)) {
                manager.requestSingleUpdate(LocationManager.NETWORK_PROVIDER, listener, Looper.getMainLooper());
            } else {
                Toast.makeText(this, "Activa la ubicación o usa Simular.", Toast.LENGTH_SHORT).show();
            }
        } catch (Exception error) {
            Toast.makeText(this, "No se pudo leer GPS. Usa Simular ubicación.", Toast.LENGTH_SHORT).show();
        }
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
        emitScanState(true, "Buscando beacons BLE cercanos…");
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

    @SuppressLint("MissingPermission")
    private void emitBeacon(ScanResult result) {
        try {
            ScanRecord record = result.getScanRecord();
            String name = null;
            if (record != null) name = record.getDeviceName();
            if ((name == null || name.trim().isEmpty()) && result.getDevice() != null) {
                try { name = result.getDevice().getName(); } catch (Exception ignored) {}
            }
            if (name == null || name.trim().isEmpty()) name = "Beacon / BLE sin nombre";

            String address = result.getDevice() != null ? result.getDevice().getAddress() : "Sin MAC";
            int rssi = result.getRssi();

            int txPower = -59;
            if (record != null && record.getTxPowerLevel() != Integer.MIN_VALUE) {
                txPower = record.getTxPowerLevel();
            }
            double distance = Math.pow(10.0, (txPower - rssi) / 20.0);

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
                    int id = data.keyAt(i);
                    byte[] bytes = data.valueAt(i);
                    manufacturer.put("id", id);
                    manufacturer.put("hex", toHex(bytes, 24));
                    manufacturers.put(manufacturer);
                }
            }

            JSONObject payload = new JSONObject();
            payload.put("name", name);
            payload.put("address", address);
            payload.put("rssi", rssi);
            payload.put("txPower", txPower);
            payload.put("distance", distance);
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
                    locate();
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
    protected void onDestroy() {
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
