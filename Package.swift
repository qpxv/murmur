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
        .target(name: "MurmurCore"),
        .target(name: "MurmurServices", dependencies: ["MurmurCore"]),
        .executableTarget(name: "MurmurApp", dependencies: ["MurmurCore", "MurmurServices"]),
        .testTarget(name: "MurmurCoreTests", dependencies: ["MurmurCore"]),
    ]
)
