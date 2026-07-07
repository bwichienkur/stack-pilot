use tauri::Manager;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .setup(|app| {
            #[cfg(desktop)]
            {
                let _tray = tauri::tray::TrayIconBuilder::new()
                    .icon(app.default_window_icon().unwrap().clone())
                    .tooltip("StackPilot")
                    .build(app)?;
            }
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            get_app_version,
            store_credential,
            get_credential
        ])
        .run(tauri::generate_context!())
        .expect("error while running StackPilot desktop");
}

#[tauri::command]
fn get_app_version() -> String {
    env!("CARGO_PKG_VERSION").to_string()
}

#[tauri::command]
fn store_credential(service: String, username: String, password: String) -> Result<(), String> {
    keyring::Entry::new(&service, &username)
        .map_err(|e| e.to_string())?
        .set_password(&password)
        .map_err(|e| e.to_string())
}

#[tauri::command]
fn get_credential(service: String, username: String) -> Result<String, String> {
    keyring::Entry::new(&service, &username)
        .map_err(|e| e.to_string())?
        .get_password()
        .map_err(|e| e.to_string())
}
