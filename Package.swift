// swift-tools-version: 6.2
import PackageDescription

// layers only point downwards: app -> services -> core. the compiler enforces it.
let package = Package(
    name: "Murmur",
    platforms: [.macOS("26.0")],
    products: [
        .executable(name: "Murmur", targets: ["MurmurApp"])
    ],
    targets: [
        // Shared is a symlink to the repo's shared/ folder, which the windows app reads too
        .target(name: "MurmurCore", resources: [.copy("Shared")]),
        .target(name: "MurmurServices", dependencies: ["MurmurCore"]),
        .executableTarget(name: "MurmurApp", dependencies: ["MurmurCore", "MurmurServices"]),
        .testTarget(name: "MurmurCoreTests", dependencies: ["MurmurCore"]),
    ]
)
