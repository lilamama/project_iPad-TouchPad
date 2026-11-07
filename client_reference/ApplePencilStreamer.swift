import UIKit
import Network

/// Low-latency UDP Streamer for Apple Pencil and Apple Pencil Pro telemetry.
/// Compatible with iOS 17.5+ (supports Barrel Roll and Squeeze) and earlier iOS versions.
@available(iOS 13.0, *)
public class ApplePencilStreamer: NSObject, UIPencilInteractionDelegate {
    private var connection: NWConnection?
    private let host: NWEndpoint.Host
    private let port: NWEndpoint.Port
    private let queue = DispatchQueue(label: "com.pencil.udp.queue", qos: .userInteractive)
    
    public var isSqueezed: Bool = false
    public var lastSqueezeTimestamp: TimeInterval = 0

    public init(host: String, port: UInt16 = 8080) {
        self.host = NWEndpoint.Host(host)
        self.port = NWEndpoint.Port(rawValue: port)!
        super.init()
        setupConnection()
    }

    private func setupConnection() {
        let params = NWParameters.udp
        params.preferNoPath = true
        connection = NWConnection(host: host, port: port, using: params)
        connection?.stateUpdateHandler = { state in
            switch state {
            case .ready:
                print("[ApplePencilStreamer] UDP stream ready to \(self.host):\(self.port)")
            case .failed(let error):
                print("[ApplePencilStreamer] Connection failed: \(error)")
            default:
                break
            }
        }
        connection?.start(queue: queue)
    }

    /// Setup Pencil Interaction for Apple Pencil 2 Double-Tap and Apple Pencil Pro Squeeze
    public func attachPencilInteraction(to view: UIView) {
        let interaction = UIPencilInteraction()
        interaction.delegate = self
        view.addInteraction(interaction)
    }

    // MARK: - UIPencilInteractionDelegate (iOS 17.5+ Squeeze & Double Tap)
    @available(iOS 17.5, *)
    public func pencilInteraction(_ interaction: UIPencilInteraction, didReceiveSqueeze squeeze: UIPencilInteraction.Squeeze) {
        let phase = squeeze.phase
        switch phase {
        case .began, .changed:
            self.isSqueezed = true
        case .ended, .cancelled:
            self.isSqueezed = false
        @unknown default:
            break
        }
    }

    // MARK: - Touch Processing (called in touchesMoved / touchesBegan in your Canvas UIView)
    public func sendTouch(_ touch: UITouch, in view: UIView, eventType: String = "move") {
        guard touch.type == .pencil || touch.type == .stylus else { return }

        let location = touch.location(in: view)
        let bounds = view.bounds

        // Normalized coordinates (0.0 to 1.0)
        let normX = Double(location.x / bounds.width)
        let normY = Double(location.y / bounds.height)

        // Force / Pressure: 0.0 to 1.0
        let pressure = touch.maximumPossibleForce > 0 
            ? Double(touch.force / touch.maximumPossibleForce) 
            : 0.0

        // Altitude & Azimuth angles
        let altitude = Double(touch.altitudeAngle)               // 0 to pi/2
        let azimuth = Double(touch.azimuthAngle(in: view))      // 0 to 2*pi

        // Apple Pencil Pro Barrel Roll (iOS 17.5+)
        var rollDegrees: Double = 0.0
        #if compiler(>=5.10)
        if #available(iOS 17.5, *) {
            let rollRadians = Double(touch.rollAngle)
            rollDegrees = (rollRadians * 180.0 / .pi).truncatingRemainder(dividingBy: 360.0)
            if rollDegrees < 0 { rollDegrees += 360.0 }
        }
        #endif

        let payload: [String: Any] = [
            "type": eventType,
            "x": normX,
            "y": normY,
            "pressure": pressure,
            "altitude": altitude,
            "azimuth": azimuth,
            "roll": rollDegrees,
            "squeeze": isSqueezed,
            "timestamp": Date().timeIntervalSince1970
        ]

        if let data = try? JSONSerialization.data(withJSONObject: payload) {
            connection?.send(content: data, completion: .idempotent)
        }
    }
}
