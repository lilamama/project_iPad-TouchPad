"""
Interactive & Automated Test Client for iPadDrawingHost
Simulates Apple Pencil / Apple Pencil Pro telemetry over UDP to port 8080.
Tests pressure ramps, tilt dynamics, barrel roll, and squeeze triggers.
"""

import socket
import json
import time
import math
import sys

UDP_IP = "127.0.0.1"
UDP_PORT = 8080

def send_payload(sock, payload):
    data = json.dumps(payload).encode('utf-8')
    sock.sendto(data, (UDP_IP, UDP_PORT))

def test_stroke(sock):
    print("\n[Test 1] Drawing an S-curve with Pressure Ramp (0.1 -> 1.0) and Tilt...")
    steps = 120
    center_x, center_y = 0.5, 0.5
    radius_x, radius_y = 0.25, 0.15

    # 1. Hover into position
    send_payload(sock, {
        "type": "hover",
        "x": center_x - radius_x,
        "y": center_y,
        "pressure": 0.0,
        "tilt_x": 0.0,
        "tilt_y": 0.0
    })
    time.sleep(0.05)

    # 2. Pen Down
    send_payload(sock, {
        "type": "down",
        "x": center_x - radius_x,
        "y": center_y,
        "pressure": 0.1,
        "tilt_x": 10.0,
        "tilt_y": -15.0
    })
    time.sleep(0.016)

    # 3. Draw curve with pressure ramp
    for i in range(steps):
        t = i / float(steps)
        angle = t * math.pi * 2.0
        x = center_x + radius_x * math.cos(angle)
        y = center_y + radius_y * math.sin(angle * 2.0)
        pressure = 0.1 + 0.85 * math.sin(t * math.pi) # 0.1 -> 0.95 -> 0.1
        tilt_x = 25.0 * math.sin(angle)
        tilt_y = 25.0 * math.cos(angle)
        roll = (t * 360.0) % 360.0 # Barrel roll simulation

        send_payload(sock, {
            "type": "move",
            "x": x,
            "y": y,
            "pressure": round(pressure, 3),
            "tilt_x": round(tilt_x, 1),
            "tilt_y": round(tilt_y, 1),
            "roll": round(roll, 1),
            "squeeze": False
        })
        time.sleep(0.008) # ~120 Hz update rate

    # 4. Pen Up
    send_payload(sock, {
        "type": "up",
        "x": center_x + radius_x,
        "y": center_y,
        "pressure": 0.0
    })
    print(" Stroke test completed successfully!")

def test_squeeze(sock):
    print("\n[Test 2] Testing Apple Pencil Pro Squeeze Trigger...")
    # Send squeeze pulse
    send_payload(sock, {
        "type": "move",
        "x": 0.5,
        "y": 0.5,
        "pressure": 0.5,
        "squeeze": True
    })
    time.sleep(0.1)
    send_payload(sock, {
        "type": "move",
        "x": 0.5,
        "y": 0.5,
        "pressure": 0.5,
        "squeeze": False
    })
    print(" Squeeze trigger sent!")

def test_hover(sock):
    print("\n[Test 3] Testing Pen Hover (Cursor Movement without Drawing)...")
    for i in range(100):
        t = i / 100.0
        x = 0.4 + 0.2 * math.cos(t * math.pi * 2)
        y = 0.4 + 0.2 * math.sin(t * math.pi * 2)
        send_payload(sock, {
            "type": "hover",
            "x": x,
            "y": y,
            "pressure": 0.0,
            "tilt_x": 0.0,
            "tilt_y": 0.0
        })
        time.sleep(0.016)
    print(" Hover test completed!")

def main():
    target_ip = sys.argv[1] if len(sys.argv) > 1 else UDP_IP
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)

    print("=======================================================")
    print(f" iPadDrawingHost UDP Test Client -> {target_ip}:{UDP_PORT}")
    print("=======================================================")
    print("1: Run Full Stroke Demo (Curve + Pressure + Tilt + Roll)")
    print("2: Run Hover Test")
    print("3: Trigger Apple Pencil Pro Squeeze")
    print("4: Continuous drawing circle loop (20 seconds)")
    print("q: Quit")

    while True:
        choice = input("\nSelect test (1-4, q): ").strip()
        if choice == '1':
            test_stroke(sock)
        elif choice == '2':
            test_hover(sock)
        elif choice == '3':
            test_squeeze(sock)
        elif choice == '4':
            print("Running 20-second continuous drawing loop. Open Photoshop/Krita now!")
            end_time = time.time() + 20.0
            while time.time() < end_time:
                test_stroke(sock)
                time.sleep(0.1)
        elif choice.lower() == 'q':
            break

if __name__ == "__main__":
    main()
