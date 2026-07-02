# Extended Reality Command Center for Remote Trekker Monitoring

A VR-based real-time monitoring system for tracking trekkers in remote, network-deficient mountainous terrain. Built using Unity and Cesium for Unity, this project renders a georeferenced 3D terrain environment where a base-station operator can monitor live trekker positions, inspect telemetry, and issue remote hazard alerts — all from inside a VR headset.

This repository contains the XR visualization subsystem. The companion IoT hardware layer (ESP32 wearable device, LoRa communication, sensor firmware) is maintained in a separate repository.

---

## What it does

- Streams real-world 3D terrain via Cesium ion, centered on the trekking region of interest
- Places live trekker markers at their correct latitude, longitude, and altitude using CesiumGlobeAnchor
- Displays floating billboard labels per trekker (name + altitude) that always face the camera
- World Space UI panels for per-trekker status (battery, altitude %, system state) and global environment overview (temperature, humidity, barometric pressure)
- Operator can select a trekker via VR ray interactor and issue a remote hazard alert
- Emergency states (SOS / fall detection) trigger automatic red marker + flashing beacon + audio alarm
- Free-flight VR rig with smooth fly-to on marker selection
- Simulated serial data source included for development and testing without live hardware

---

## Tech Stack

| Layer | Tools |
|---|---|
| Engine | Unity (LTS) |
| Geospatial | Cesium for Unity, Cesium ion |
| VR | Unity XR Interaction Toolkit, OpenXR |
| Scripting | C# (.NET) |
| Communication | System.IO.Ports (serial ingestion from LoRa master node) |

---
