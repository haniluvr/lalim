// Usage: swift Tools/make_seamless_loop.swift <in.mp4> <out.mp4> <fadeSeconds> <width> <height>
// e.g.   swift Tools/make_seamless_loop.swift ~/Downloads/clip.mp4 Assets/Lalim/Videos/Clip_Loop.mp4 1.0 720 1280

// Bakes a seamless loop: the clip's last `fade` seconds cross-fade into its first `fade` seconds,
// so the output's final frame flows straight into its first. Output = (duration - fade), no audio.
import AVFoundation
let src = URL(fileURLWithPath: CommandLine.arguments[1])
let dst = URL(fileURLWithPath: CommandLine.arguments[2])
let fade = Double(CommandLine.arguments[3])!
let outW = Double(CommandLine.arguments[4])!, outH = Double(CommandLine.arguments[5])!
try? FileManager.default.removeItem(at: dst)
let sem = DispatchSemaphore(value: 0)
Task {
  do {
    let asset = AVURLAsset(url: src)
    let dur = try await asset.load(.duration)
    let vt = try await asset.loadTracks(withMediaType: .video)[0]
    let natural = try await vt.load(.naturalSize)
    let pref = try await vt.load(.preferredTransform)
    let fps = try await vt.load(.nominalFrameRate)
    let ts: CMTimeScale = 600
    let D = CMTime(seconds: dur.seconds, preferredTimescale: ts)
    let F = CMTime(seconds: fade, preferredTimescale: ts)
    let body = CMTimeSubtract(D, F)                     // output length

    let comp = AVMutableComposition()
    let head = comp.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid)!
    let tail = comp.addMutableTrack(withMediaType: .video, preferredTrackID: kCMPersistentTrackID_Invalid)!
    try head.insertTimeRange(CMTimeRange(start: .zero, duration: body), of: vt, at: .zero)   // source [0, D-F)
    try tail.insertTimeRange(CMTimeRange(start: body, duration: F), of: vt, at: .zero)       // source [D-F, D) over output [0, F)

    // Fit the (possibly rotated) source into outW×outH.
    let r = CGRect(origin: .zero, size: natural).applying(pref)
    let sx = outW / abs(r.width), sy = outH / abs(r.height)
    let xf = pref.concatenating(CGAffineTransform(translationX: -r.minX, y: -r.minY)).concatenating(CGAffineTransform(scaleX: sx, y: sy))

    let tailLayer = AVMutableVideoCompositionLayerInstruction(assetTrack: tail)
    tailLayer.setTransform(xf, at: .zero)
    tailLayer.setOpacityRamp(fromStartOpacity: 1, toEndOpacity: 0, timeRange: CMTimeRange(start: .zero, duration: F))
    let headLayer1 = AVMutableVideoCompositionLayerInstruction(assetTrack: head)
    headLayer1.setTransform(xf, at: .zero)
    let blend = AVMutableVideoCompositionInstruction()
    blend.timeRange = CMTimeRange(start: .zero, duration: F)
    blend.layerInstructions = [tailLayer, headLayer1]      // first = on top

    let headLayer2 = AVMutableVideoCompositionLayerInstruction(assetTrack: head)
    headLayer2.setTransform(xf, at: .zero)
    let rest = AVMutableVideoCompositionInstruction()
    rest.timeRange = CMTimeRange(start: F, duration: CMTimeSubtract(body, F))
    rest.layerInstructions = [headLayer2]

    let vc = AVMutableVideoComposition()
    vc.renderSize = CGSize(width: outW, height: outH)
    vc.frameDuration = CMTime(value: 1, timescale: CMTimeScale(fps.rounded()))
    vc.instructions = [blend, rest]

    let ex = AVAssetExportSession(asset: comp, presetName: AVAssetExportPresetHighestQuality)!
    ex.videoComposition = vc
    try await ex.export(to: dst, as: .mp4)
    print("ok", "in", dur.seconds, "s ->", body.seconds, "s", Int(outW), "x", Int(outH), "@", fps)
  } catch { print("ERROR", error) }
  sem.signal()
}
sem.wait()
