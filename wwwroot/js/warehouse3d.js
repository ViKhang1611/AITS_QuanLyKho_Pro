/**
 * Warehouse3D.js - Three.js WebGL 3D Warehouse Visualization with AGV Robot Integration
 * Synchronized with Zone Colors, Lighter Translucent Grey for Empty Bins & AGV Robot Path Navigation
 */

(function () {
    const instances = {};

    // Three.js r128 assumes every morph-target entry is an attribute with a
    // name. This GLB contains empty entries, which otherwise makes GLTFLoader
    // throw "Cannot read properties of null (reading 'name')" before it can
    // add the robot to the scene.
    if (typeof THREE !== "undefined" && THREE.Mesh && THREE.Mesh.prototype) {
        const origUpdateMorphTargets = THREE.Mesh.prototype.updateMorphTargets;
        THREE.Mesh.prototype.updateMorphTargets = function () {
            try {
                if (this.geometry && this.geometry.morphAttributes) {
                    for (const key in this.geometry.morphAttributes) {
                        const list = this.geometry.morphAttributes[key];
                        if (Array.isArray(list)) {
                            const attributes = list.filter(Boolean);
                            this.geometry.morphAttributes[key] = attributes;
                            attributes.forEach((attr, idx) => {
                                if (!attr.name) attr.name = key + "_" + idx;
                            });
                        }
                    }
                }
                return origUpdateMorphTargets.apply(this, arguments);
            } catch (e) {
                console.warn("[Warehouse3D] Safe catch updateMorphTargets:", e);
            }
        };
    }

    // =========================================================================
    // ⚙️ BẢNG THÔNG SỐ KÍCH THƯỚC KHO & ROBOT THỰC TẾ (REAL-WORLD SCALE CONFIG)
    // -------------------------------------------------------------------------
    // HƯỚNG DẪN TÙY CHỈNH DÀNH CHO NGƯỜI DÙNG / LẬP TRÌNH VIÊN SAU NÀY:
    // Wszystkie kích thước dưới đây được quy đổi chuẩn theo đơn vị MÉT (m).
    // Khi bạn muốn thay đổi mô hình xe Robot AGV khác hoặc thay đổi kích thước kho:
    //
    // 1️⃣ KÍCH THƯỚC Ô KỆ VÀ DÃY KHO (WAREHOUSE RACK & BIN DIMENSIONS):
    //    - BIN_W: Chiều ngang 1 ô kệ chứa hàng (m). Chuẩn Pallet Euro/ISO ~ 1.40m
    //    - BIN_H: Chiều cao lọt lòng mỗi tầng kệ (m). Chuẩn tầng kệ lưu trữ ~ 1.20m
    //    - BIN_D: Chiều sâu khung kệ chứa hàng (m). Chuẩn giá kệ Pallet ~ 1.10m
    //    - BAY_GAP: Khe hở giữa các cột/ô kệ trong cùng 1 dãy (m). ~ 0.20m
    //    - AISLE_GAP: Chiều rộng lối đi AGV giữa 2 dãy kệ (m). Chuẩn AGV ~ 3.00m
    //    - LEVEL_GAP: Độ dày dầm ngang (beam) đỡ sàn ô kệ (m). ~ 0.25m
    //
    // 2️⃣ THÔNG SỐ XE ROBOT AGV (AGV ROBOT CONFIGURATION):
    //    - ROBOT_TARGET_HEIGHT: Chiều cao xe AGV thực tế sau scale (m). Chuẩn ~ 0.85m
    //    - ROBOT_X_OFFSET: Khoảng cách xe đứng lùi ra khỏi tim kệ để chạy giữa lối đi (m). ~ 1.00m
    //    - ROBOT_CORRIDOR_Z: Tọa độ hành lang chính chạy ngang trước các dãy kệ (m). ~ -1.60m
    //    - ROBOT_SPEED: Tốc độ di chuyển thực tế của AGV (m/s). ~ 3.5 m/s
    //    - ROBOT_SCALE_FALLBACK: Hệ số tỉ lệ nhân bổ sung nếu file 3D .glb quá to/nhỏ. Default = 1.0
    //
    // 3️⃣ THÔNG SỐ MẶC ĐỊNH GÓC CAM & NƠI TẬP TRUNG (INITIAL CAMERA ANGLE):
    //    - Ngay khi tải trang, Camera tự động căn góc nhìn 3/4 trực diện vào vị trí của Robot AGV.
    // =========================================================================

    // --- 1. Kích thước kệ kho tiêu chuẩn thực tế (m) ---
    const BIN_W = 1.40;        // m - Chiều ngang ô kệ / Pallet thực tế
    const BIN_H = 1.20;        // m - Chiều cao tầng lưu trữ thực tế
    const BIN_D = 1.10;        // m - Chiều sâu khung kệ thực tế
    const BAY_GAP = 0.20;      // m - Khoảng cách giữa các ô kệ liền kề
    const AISLE_GAP = 5.00;    // m - Chiều rộng khoảng cách giữa các dãy kệ (Lối đi rộng 3.60m thông thoáng)
    const LEVEL_GAP = 0.25;    // m - Độ dày dầm đỡ tầng kệ

    // --- 2. Màu sắc kệ ---
    const EMPTY_COLOR = 0x94A3B8;    // Màu kệ trống (Slate gray)
    const WARNING_COLOR = 0xEF4444;  // Màu cảnh báo hạn sử dụng (Red)

    // --- 3. Thông số di chuyển & Kích thước Robot AGV ---
    const ROBOT_SPEED = 3.5;          // m/s - Tốc độ di chuyển AGV
    const ROBOT_X_OFFSET = 2.00;      // m - Khoảng cách an toàn đứng cách tim kệ (nằm chính giữa lối đi, rộng rãi thông thoáng)
    const ROBOT_CORRIDOR_Z = -2.00;   // m - Tọa độ hành lang ngang chính TRƯỚC các dãy kệ
    const REAR_CORRIDOR_MARGIN = 2.00;// m - Khoảng cách lùi an toàn HÀNH LANG SAU KHO (Đủ rộng 1.8m để Robot xoay vòng quay đầu thoải mái không va đụng kệ)
    const ROBOT_MODEL_URL = "/models/robot.glb";
    const ROBOT_WALK_CLIP = "Motion";
    const ROBOT_TARGET_HEIGHT = 0.85; // m - Chiều cao tiêu chuẩn thực tế của Robot AGV
    const ROBOT_SCALE_FALLBACK = 1.0; // Tỉ lệ scale nhân thêm nếu file .glb khác kích thước

    function getBinColor(bin) {
        if (bin.status === "empty" || !bin.materialName) {
            return new THREE.Color(EMPTY_COLOR);
        }
        if (bin.isExpiring || bin.status === "expiring") {
            return new THREE.Color(WARNING_COLOR);
        }
        return new THREE.Color(bin.zoneColor || "#3b82f6");
    }

    function getBinOpacity(bin, isMatchFilter) {
        if (!isMatchFilter) return 0.08;
        if (bin.status === "empty" || !bin.materialName) return 0.20;
        return 0.85;
    }

    function getBinEmissiveIntensity(bin, isMatchFilter) {
        if (!isMatchFilter) return 0.01;
        if (bin.status === "empty" || !bin.materialName) return 0.02;
        return 0.25;
    }

    function isBinMatchingFilter(bin, filterStatus) {
        if (filterStatus === "all") return true;
        if (filterStatus === "expiring") return bin.isExpiring === true;
        if (filterStatus === "empty") return bin.status === "empty" || !bin.materialName;
        return true;
    }

    function computePath(from, to, maxBay = 4) {
        const sameAisle = Math.abs(from.x - to.x) <= 0.05;

        // 1. Đi cùng dãy kệ: đi thẳng trực tiếp dọc theo lối đi của dãy đó
        if (sameAisle) {
            return [{ x: to.x, z: to.z }];
        }

        // 2. Chuyển sang dãy kệ khác: Tự động so sánh khoảng cách giữa 2 tuyến đường:
        //    - Tuyến A: Chạy vòng qua Hành Lang TRƯỚC (Front Corridor)
        //    - Tuyến B: Chạy vòng qua Hành Lang SAU (Rear Corridor)
        const frontCorridorZ = ROBOT_CORRIDOR_Z;
        const rearCorridorZ = maxBay * (BIN_D + BAY_GAP) + BIN_D / 2 + REAR_CORRIDOR_MARGIN;

        const distFront = Math.abs(from.z - frontCorridorZ) + Math.abs(to.x - from.x) + Math.abs(to.z - frontCorridorZ);
        const distRear = Math.abs(from.z - rearCorridorZ) + Math.abs(to.x - from.x) + Math.abs(to.z - rearCorridorZ);

        const points = [];

        if (distRear < distFront) {
            // 🚀 ĐƯỜNG ĐI NGẮN NHẤT: Chạy qua Hành Lang SAU kho (Tối ưu tiết kiệm thời gian)
            if (Math.abs(from.z - rearCorridorZ) > 0.05) {
                points.push({ x: from.x, z: rearCorridorZ });
            }
            points.push({ x: to.x, z: rearCorridorZ });
            if (Math.abs(to.z - rearCorridorZ) > 0.05) {
                points.push({ x: to.x, z: to.z });
            }
        } else {
            // 🚀 ĐƯỜNG ĐI NGẮN NHẤT: Chạy qua Hành Lang TRƯỚC kho
            if (Math.abs(from.z - frontCorridorZ) > 0.05) {
                points.push({ x: from.x, z: frontCorridorZ });
            }
            points.push({ x: to.x, z: frontCorridorZ });
            if (Math.abs(to.z - frontCorridorZ) > 0.05) {
                points.push({ x: to.x, z: to.z });
            }
        }
        return points;
    }

    function binWorldPos(bin) {
        return {
            x: bin.aisle * AISLE_GAP,
            y: bin.level * (BIN_H + LEVEL_GAP) + BIN_H / 2,
            z: bin.bay * (BIN_D + BAY_GAP)
        };
    }

    function robotTargetFor(bin) {
        return {
            x: bin.aisle * AISLE_GAP - ROBOT_X_OFFSET,
            z: bin.bay * (BIN_D + BAY_GAP)
        };
    }

    function removeInvalidRobotAnimationChannels(glbBuffer) {
        const view = new DataView(glbBuffer);
        const GLB_MAGIC = 0x46546C67;
        const JSON_CHUNK_TYPE = 0x4E4F534A;
        const headerSize = 12;
        const chunkHeaderSize = 8;

        if (glbBuffer.byteLength < headerSize + chunkHeaderSize ||
            view.getUint32(0, true) !== GLB_MAGIC ||
            view.getUint32(headerSize + 4, true) !== JSON_CHUNK_TYPE) {
            return glbBuffer;
        }

        const jsonLength = view.getUint32(headerSize, true);
        const jsonStart = headerSize + chunkHeaderSize;
        const jsonEnd = jsonStart + jsonLength;
        if (jsonEnd > glbBuffer.byteLength) return glbBuffer;

        let document;
        try {
            const json = new TextDecoder().decode(new Uint8Array(glbBuffer, jsonStart, jsonLength)).trim();
            document = JSON.parse(json);
        } catch (_) {
            return glbBuffer;
        }

        let removedChannelCount = 0;
        (document.animations || []).forEach((animation) => {
            const originalSamplers = animation.samplers || [];
            const samplerMap = [];
            const validSamplers = [];

            originalSamplers.forEach((sampler, index) => {
                const output = document.accessors && document.accessors[sampler.output];
                const hasOutputData = output &&
                    (output.bufferView !== undefined && output.bufferView !== null ||
                     output.sparse !== undefined && output.sparse !== null);

                if (hasOutputData) {
                    samplerMap[index] = validSamplers.length;
                    validSamplers.push(sampler);
                } else {
                    samplerMap[index] = -1;
                }
            });

            const originalChannels = animation.channels || [];
            animation.channels = originalChannels
                .filter((channel) => samplerMap[channel.sampler] >= 0)
                .map((channel) => ({
                    ...channel,
                    sampler: samplerMap[channel.sampler]
                }));
            animation.samplers = validSamplers;
            removedChannelCount += originalChannels.length - animation.channels.length;
        });

        if (removedChannelCount === 0) return glbBuffer;

        const jsonBytes = new TextEncoder().encode(JSON.stringify(document));
        const paddedJsonLength = Math.ceil(jsonBytes.length / 4) * 4;
        const remainingChunks = new Uint8Array(glbBuffer, jsonEnd);
        const output = new Uint8Array(headerSize + chunkHeaderSize + paddedJsonLength + remainingChunks.length);
        const outputView = new DataView(output.buffer);

        outputView.setUint32(0, GLB_MAGIC, true);
        outputView.setUint32(4, view.getUint32(4, true), true);
        outputView.setUint32(8, output.byteLength, true);
        outputView.setUint32(headerSize, paddedJsonLength, true);
        outputView.setUint32(headerSize + 4, JSON_CHUNK_TYPE, true);
        output.set(jsonBytes, jsonStart);
        output.fill(0x20, jsonStart + jsonBytes.length, jsonStart + paddedJsonLength);
        output.set(remainingChunks, headerSize + chunkHeaderSize + paddedJsonLength);

        console.info("[Warehouse3D] Ignored invalid robot animation channels:", removedChannelCount);
        return output.buffer;
    }

    function initWarehouse3D(containerId, binsData, dotNetHelper, filterStatus = "all") {
        const container = document.getElementById(containerId);
        if (!container) return;

        if (instances[containerId]) {
            instances[containerId].destroy();
        }

        const width = container.clientWidth || 800;
        const height = container.clientHeight || 500;

        // 1. Scene setup
        const scene = new THREE.Scene();
        scene.background = new THREE.Color(0x0B0F17);

        // 2. Camera setup & Initial Focus on AGV Robot
        const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);

        let maxAisle = 0, maxBay = 0, maxLevel = 0;
        binsData.forEach(b => {
            if (b.aisle > maxAisle) maxAisle = b.aisle;
            if (b.bay > maxBay) maxBay = b.bay;
            if (b.level > maxLevel) maxLevel = b.level;
        });

        const centerX = (maxAisle * AISLE_GAP) / 2;
        const centerY = (maxLevel * (BIN_H + LEVEL_GAP)) / 2;
        const centerZ = (maxBay * (BIN_D + BAY_GAP)) / 2;

        // =========================================================================
        // 🎯 TỌA ĐỘ VÀ GÓC CAMERA TRỰC DIỆN VÀO ROBOT & QUAN SÁT TOÀN BỘ KHO:
        // - Vị trí xuất phát Robot: X = -ROBOT_X_OFFSET (-1.00m), Z = ROBOT_CORRIDOR_Z (-1.60m)
        // - Đặt Camera phía trước-bên trái kho (X < 0, Z < 0) hướng ống kính vào kho (X > 0, Z > 0)
        // - Kết quả: Thấy sắc nét Robot AGV ở cận cảnh phía trước và toàn bộ khung kho phía sau.
        // =========================================================================
        const robotStartPosX = -ROBOT_X_OFFSET;
        const robotStartPosY = ROBOT_TARGET_HEIGHT * 0.5; // Tâm điểm robot
        const robotStartPosZ = ROBOT_CORRIDOR_Z;

        // Đặt vị trí Camera phía trước bên trái Robot
        camera.position.set(
            robotStartPosX - 7.0,  // X = -9.0m (Rộng rãi)
            robotStartPosY + 5.5,  // Y = 5.9m (Rất cao - nhìn toàn cảnh kho)
            robotStartPosZ - 7.0   // Z = -9.0m (Rộng rãi)
        );

        // 3. Renderer setup
        const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
        renderer.setSize(width, height);
        renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        renderer.shadowMap.enabled = true;
        container.innerHTML = "";
        container.appendChild(renderer.domElement);

        // 4. OrbitControls - Căn góc ngắm qua Robot hướng thẳng vào các dãy kệ kho
        const controls = new THREE.OrbitControls(camera, renderer.domElement);
        controls.enableDamping = true;
        controls.dampingFactor = 0.05;
        controls.maxPolarAngle = Math.PI / 2.02;
        controls.minDistance = 1.5;
        controls.maxDistance = Math.max((maxAisle + 1) * AISLE_GAP * 3, 120);
        
        // Hướng tâm nhìn qua vị trí Robot vào trung tâm kho rộng rãi
        controls.target.set(Math.max(centerX * 0.45, 2.5), centerY * 0.8, Math.max(centerZ * 0.45, 2.0));
        controls.update();

        // 5. Lighting
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.75);
        scene.add(ambientLight);

        const dirLight1 = new THREE.DirectionalLight(0xffffff, 0.85);
        dirLight1.position.set(centerX + 10, centerY + 15, centerZ + 10);
        scene.add(dirLight1);

        const dirLight2 = new THREE.DirectionalLight(0x6B93FF, 0.35);
        dirLight2.position.set(centerX - 10, centerY + 10, centerZ - 10);
        scene.add(dirLight2);

        // 6. Floor & Grid
        const floorW = (maxAisle + 1) * AISLE_GAP + 8;
        const floorD = (maxBay + 1) * (BIN_D + BAY_GAP) + 8;

        const floorGeo = new THREE.PlaneGeometry(floorW, floorD);
        const floorMat = new THREE.MeshStandardMaterial({ color: 0x0B0F17, roughness: 0.8 });
        const floorMesh = new THREE.Mesh(floorGeo, floorMat);
        floorMesh.rotation.x = -Math.PI / 2;
        floorMesh.position.set(centerX, -0.01, centerZ);
        scene.add(floorMesh);

        const gridHelper = new THREE.GridHelper(Math.max(floorW, floorD), 32, 0x2A3446, 0x1C2432);
        gridHelper.position.set(centerX, 0, centerZ);
        scene.add(gridHelper);

        // 7. Rack metal posts (Khung cột thép chân kệ kho công nghiệp thực tế)
        // Cột thép được đặt ở 4 góc ngoài bao quanh dãy kệ (xLeft, xRight, zFront, zRear), tuyệt đối không đâm xuyên hay che chắn ô chứa hàng
        const postMat = new THREE.MeshStandardMaterial({ color: 0x334155, metalness: 0.6, roughness: 0.4 });
        const rackBeamMat = new THREE.MeshStandardMaterial({ color: 0x1E293B, metalness: 0.7, roughness: 0.3 });
        const postHeight = (maxLevel + 1) * (BIN_H + LEVEL_GAP) + 0.15;
        const postSize = 0.06;
        const postGeo = new THREE.BoxGeometry(postSize, postHeight, postSize);

        for (let a = 0; a <= maxAisle; a++) {
            const aisleX = a * AISLE_GAP;
            const xLeft = aisleX - BIN_W / 2 - postSize / 2 - 0.03;
            const xRight = aisleX + BIN_W / 2 + postSize / 2 + 0.03;
            const zFront = -BIN_D / 2 - 0.03;
            const zRear = maxBay * (BIN_D + BAY_GAP) + BIN_D / 2 + 0.03;

            // 4 cột đứng đặt ở 4 góc ngoài của từng dãy kệ
            const postFL = new THREE.Mesh(postGeo, postMat);
            postFL.position.set(xLeft, postHeight / 2, zFront);
            scene.add(postFL);

            const postFR = new THREE.Mesh(postGeo, postMat);
            postFR.position.set(xRight, postHeight / 2, zFront);
            scene.add(postFR);

            const postRL = new THREE.Mesh(postGeo, postMat);
            postRL.position.set(xLeft, postHeight / 2, zRear);
            scene.add(postRL);

            const postRR = new THREE.Mesh(postGeo, postMat);
            postRR.position.set(xRight, postHeight / 2, zRear);
            scene.add(postRR);

            // Thanh dầm đỡ ngang (Beam) chạy dọc bao quanh khung kệ
            for (let l = 0; l <= maxLevel + 1; l++) {
                const yBeam = l * (BIN_H + LEVEL_GAP);
                const beamW = BIN_W + 0.14;
                const beamGeo = new THREE.BoxGeometry(beamW, 0.04, 0.04);

                const beamF = new THREE.Mesh(beamGeo, rackBeamMat);
                beamF.position.set(aisleX, yBeam, zFront);
                scene.add(beamF);

                const beamR = new THREE.Mesh(beamGeo, rackBeamMat);
                beamR.position.set(aisleX, yBeam, zRear);
                scene.add(beamR);
            }
        }

        // 8. Bins creation
        const binGroup = new THREE.Group();
        scene.add(binGroup);

        const binMeshMap = new Map();
        const boxGeo = new THREE.BoxGeometry(BIN_W, BIN_H, BIN_D);

        binsData.forEach(bin => {
            const x = bin.aisle * AISLE_GAP;
            const y = bin.level * (BIN_H + LEVEL_GAP) + BIN_H / 2;
            const z = bin.bay * (BIN_D + BAY_GAP);

            const isMatchFilter = isBinMatchingFilter(bin, filterStatus);
            const binColor = getBinColor(bin);
            const opacity = getBinOpacity(bin, isMatchFilter);
            const emissiveInt = getBinEmissiveIntensity(bin, isMatchFilter);

            const mat = new THREE.MeshStandardMaterial({
                color: binColor,
                transparent: true,
                opacity: opacity,
                emissive: binColor,
                emissiveIntensity: emissiveInt
            });

            const mesh = new THREE.Mesh(boxGeo, mat);
            mesh.position.set(x, y, z);
            mesh.userData = bin;

            // Wireframe edge
            const edgesGeo = new THREE.EdgesGeometry(boxGeo);
            const isEmptyBin = bin.status === "empty" || !bin.materialName;
            const lineMat = new THREE.LineBasicMaterial({
                color: isMatchFilter ? (isEmptyBin ? 0x64748B : 0x0F1420) : 0x1C2432,
                linewidth: 1
            });
            const wireframe = new THREE.LineSegments(edgesGeo, lineMat);
            mesh.add(wireframe);

            binGroup.add(mesh);
            binMeshMap.set(bin.id, mesh);
        });

        // 9. Target Beacon & Arrival Light Beam
        const beaconGroup = new THREE.Group();
        beaconGroup.visible = false;
        scene.add(beaconGroup);

        // Vertical light beam cylinder
        const beamGeo = new THREE.CylinderGeometry(0.04, 0.25, 8, 16, 1, true);
        const beamMat = new THREE.MeshBasicMaterial({
            color: 0x38BDF8,
            transparent: true,
            opacity: 0.35,
            side: THREE.DoubleSide,
            depthWrite: false
        });
        const beamMesh = new THREE.Mesh(beamGeo, beamMat);
        beamMesh.position.set(0, 4, 0);
        beaconGroup.add(beamMesh);

        // Top pointer cone
        const coneGeo = new THREE.ConeGeometry(0.18, 0.35, 4);
        const coneMat = new THREE.MeshBasicMaterial({ color: 0x38BDF8 });
        const coneMesh = new THREE.Mesh(coneGeo, coneMat);
        coneMesh.rotation.x = Math.PI;
        coneMesh.position.set(0, BIN_H / 2 + 0.3, 0);
        beaconGroup.add(coneMesh);

        // 10. AGV Robot Setup & GLTF Loader
        const startX = -ROBOT_X_OFFSET;
        const startZ = ROBOT_CORRIDOR_Z;

        const robotGroup = new THREE.Group();
        robotGroup.position.set(startX, 0, startZ);
        scene.add(robotGroup);

        // Add spotlight / glow light on robot
        const robotLight = new THREE.PointLight(0x38BDF8, 4.0, 10);
        robotLight.position.set(0, 1.2, 0);
        robotGroup.add(robotLight);

        // Ground glow ring under robot
        const groundRingGeo = new THREE.RingGeometry(0.55, 0.75, 32);
        const groundRingMat = new THREE.MeshBasicMaterial({
            color: 0x38BDF8,
            side: THREE.DoubleSide,
            transparent: true,
            opacity: 0.95,
            blending: THREE.AdditiveBlending,
            depthWrite: false
        });
        const groundRingMesh = new THREE.Mesh(groundRingGeo, groundRingMat);
        groundRingMesh.rotation.x = -Math.PI / 2;
        groundRingMesh.position.y = 0.02;
        robotGroup.add(groundRingMesh);

        let robotMixer = null;
        let walkAction = null;
        let isRobotMoving = false;
        let robotPath = [];
        let robotWaypointIndex = 0;
        let targetBinData = null;
        let currentRobotPos = { x: startX, z: startZ };
        let robotPathLine = null;

        function updateRobotPathLine() {
            const points = [{ x: currentRobotPos.x, z: currentRobotPos.z }, ...robotPath.slice(Math.max(0, robotWaypointIndex))];

            if (!robotPathLine) {
                const geometry = new THREE.BufferGeometry();
                const material = new THREE.LineBasicMaterial({
                    color: 0x38BDF8,
                    transparent: true,
                    opacity: 1,
                    depthWrite: false
                });
                robotPathLine = new THREE.Line(geometry, material);
                robotPathLine.frustumCulled = false;
                scene.add(robotPathLine);
            }

            if (points.length < 2 || (!isRobotMoving && robotPath.length === 0)) {
                robotPathLine.visible = false;
                return;
            }

            const positionArray = new Float32Array(points.length * 3);
            points.forEach((point, index) => {
                const i = index * 3;
                positionArray[i] = point.x;
                positionArray[i + 1] = 0.12;
                positionArray[i + 2] = point.z;
            });

            robotPathLine.geometry.setAttribute("position", new THREE.BufferAttribute(positionArray, 3));
            robotPathLine.geometry.setDrawRange(0, points.length);
            robotPathLine.geometry.computeBoundingSphere();
            robotPathLine.visible = true;
            robotPathLine.material.opacity = isRobotMoving ? 1 : 0.55;
            robotPathLine.material.color.setHex(0x38BDF8);

            window.__warehouseDebug = {
                robotPathLine,
                robotPath,
                currentRobotPos,
                robotWaypointIndex,
                isRobotMoving,
                points
            };
        }

        // Load the production robot model.
        let robotModel = null;
        let isDestroyed = false;
        if (typeof THREE.GLTFLoader === "undefined") {
            console.error("[Warehouse3D] GLTFLoader is unavailable; cannot load the robot model.");
        } else {
            const loader = new THREE.GLTFLoader();
            if (typeof THREE.DRACOLoader !== "undefined") {
                const dracoLoader = new THREE.DRACOLoader();
                dracoLoader.setDecoderPath("https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/libs/draco/gltf/");
                loader.setDRACOLoader(dracoLoader);
            }
            const onRobotLoaded = (gltf) => {
                if (isDestroyed) return;

                robotModel = gltf.scene;
                robotModel.updateMatrixWorld(true);

                const bounds = new THREE.Box3().setFromObject(robotModel);
                const size = new THREE.Vector3();
                const center = new THREE.Vector3();
                bounds.getSize(size);
                bounds.getCenter(center);

                if (size.y <= 0) {
                    console.error("[Warehouse3D] The robot model has an invalid height.");
                    return;
                }

                // =========================================================================
                // 📏 THUẬT TOÁN TỰ ĐỘNG SCALE VÀ CĂN ĐẾ CHUẨN MỌI CON ROBOT 3D:
                // - size.y: Chiều cao gốc của file model 3D .glb
                // - ROBOT_TARGET_HEIGHT: Chiều cao mong muốn thực tế trong kho (m) (Default: 0.85m)
                // - ROBOT_SCALE_FALLBACK: Tỉ lệ nhân bổ sung nếu cần điều chỉnh thêm.
                // Khi bạn thay file robot.glb mới, thuật toán này sẽ TỰ ĐỘNG đo và quy đổi
                // đúng chiều cao ROBOT_TARGET_HEIGHT mà không bị méo hay bị âm dưới mặt đất.
                // =========================================================================
                const robotScale = (ROBOT_TARGET_HEIGHT / size.y) * ROBOT_SCALE_FALLBACK;
                robotModel.scale.setScalar(robotScale);

                // Sau khi scale, tính lại bounds để đặt chân đế robot chuẩn ngay mặt đất y=0
                const scaledBounds = new THREE.Box3().setFromObject(robotModel);
                const scaledMinY = scaledBounds.min.y;

                robotModel.position.set(
                    -center.x,
                    -scaledMinY,
                    -center.z
                );
                robotModel.traverse((child) => {
                    if (child.isMesh) {
                        child.castShadow = true;
                        child.receiveShadow = true;
                    }
                });
                robotGroup.add(robotModel);

                // Setup animation mixer
                if (gltf.animations && gltf.animations.length > 0) {
                    robotMixer = new THREE.AnimationMixer(robotModel);
                    const clipName = ROBOT_WALK_CLIP;
                    const clip = gltf.animations.find(a => a.name === clipName) || gltf.animations[0];
                    if (clip) {
                        walkAction = robotMixer.clipAction(clip);
                        walkAction.play();
                        walkAction.timeScale = isRobotMoving ? 1 : 0;
                    }
                }
                console.info("[Warehouse3D] Robot model loaded successfully.", {
                    clip: walkAction ? walkAction.getClip().name : null,
                    rawHeight: size.y,
                    targetHeight: ROBOT_TARGET_HEIGHT,
                    scale: robotScale
                });
            };
            const onRobotLoadError = (err) => console.error("[Warehouse3D] Failed to load robot model.", err);

            fetch(ROBOT_MODEL_URL)
                .then((response) => {
                    if (!response.ok) throw new Error(`HTTP ${response.status} while loading ${ROBOT_MODEL_URL}`);
                    return response.arrayBuffer();
                })
                .then(
                    (buffer) => loader.parse(
                        removeInvalidRobotAnimationChannels(buffer),
                        "/models/",
                        onRobotLoaded,
                        onRobotLoadError
                    ),
                    onRobotLoadError
                );
        }

        // 10.5 Floating 3D Search Panel attached to Robot Head
        const robotLabelDiv = document.createElement("div");
        robotLabelDiv.className = "robot-3d-tooltip";
        robotLabelDiv.style.cssText = "position:absolute;top:0;left:0;pointer-events:auto;z-index:15;font-family:Inter,system-ui,sans-serif;width:220px;";
        robotLabelDiv.innerHTML = `
            <div style="background:rgba(13,18,32,0.95);border:1px solid #232C3D;border-radius:10px;padding:8px;box-shadow:0 8px 24px rgba(0,0,0,0.6);backdrop-filter:blur(6px);">
                <div style="display:flex;align-items:center;gap:6px;">
                    <span style="color:#5B93E5;font-size:12px;">🔍</span>
                    <input class="robot-search-input" type="text" placeholder="Nhập SKU hoặc quét mã QR kệ..." style="flex:1;min-width:0;background:transparent;border:none;outline:none;color:#E2E8F0;font-size:11.5px;font-family:inherit;" />
                </div>
                <div class="robot-search-results" style="display:none;border-top:1px solid #1B2330;margin-top:6px;padding-top:5px;max-height:140px;overflow-y:auto;"></div>
            </div>
            <div style="width:0;height:0;margin:0 auto;border-left:6px solid transparent;border-right:6px solid transparent;border-top:6px solid rgba(13,18,32,0.95);"></div>
        `;
        if (container) {
            container.style.position = "relative";
            container.appendChild(robotLabelDiv);
        }

        const searchInput = robotLabelDiv.querySelector(".robot-search-input");
        const searchResultsDiv = robotLabelDiv.querySelector(".robot-search-results");

        if (searchInput && searchResultsDiv) {
            searchInput.addEventListener("input", (e) => {
                const q = e.target.value.trim().toLowerCase();
                if (!q) {
                    searchResultsDiv.style.display = "none";
                    searchResultsDiv.innerHTML = "";
                    return;
                }
                const matches = binsData.filter(b =>
                    (b.materialName && b.materialName.toLowerCase().includes(q)) ||
                    (b.materialCode && b.materialCode.toLowerCase().includes(q)) ||
                    (b.id && b.id.toLowerCase().includes(q))
                ).slice(0, 5);

                if (matches.length > 0) {
                    searchResultsDiv.style.display = "block";
                    searchResultsDiv.innerHTML = matches.map(m => `
                        <div class="robot-search-item" data-binid="${m.id}" style="display:flex;justify-content:space-between;padding:5px 4px;font-size:11px;color:#CBD5E1;cursor:pointer;border-radius:4px;margin-bottom:2px;" onmouseover="this.style.background='#1B2433'" onmouseout="this.style.background='transparent'">
                            <span>${m.materialName || 'Ô trống'}</span>
                            <span style="color:#64748B;font-family:monospace;font-size:10px;">${m.id}</span>
                        </div>
                    `).join("");

                    searchResultsDiv.querySelectorAll(".robot-search-item").forEach(el => {
                        el.addEventListener("click", () => {
                            const binId = el.getAttribute("data-binid");
                            const bin = binMeshMap.get(binId);
                            if (bin) {
                                dispatchRobotToBin(bin.userData);
                                if (selectedMesh && selectedMesh !== bin) resetMeshStyle(selectedMesh);
                                selectedMesh = bin;
                                highlightMesh(selectedMesh, 1.12, 0.98, 0.65);
                                if (dotNetHelper) dotNetHelper.invokeMethodAsync("OnBinSelected", bin.userData);
                            }
                            searchResultsDiv.style.display = "none";
                            searchInput.value = "";
                        });
                    });
                } else {
                    searchResultsDiv.style.display = "block";
                    searchResultsDiv.innerHTML = `<div style="font-size:10.5px;color:#64748B;padding:4px;">Không tìm thấy kết quả</div>`;
                }
            });
        }

        const tempV = new THREE.Vector3();
        function updateRobotLabelPosition() {
            if (!robotGroup || !robotLabelDiv || !camera) return;
            tempV.set(currentRobotPos.x, ROBOT_TARGET_HEIGHT + 0.65, currentRobotPos.z);
            tempV.project(camera);

            const x = (tempV.x * 0.5 + 0.5) * (container.clientWidth || width);
            const y = (tempV.y * -0.5 + 0.5) * (container.clientHeight || height);

            robotLabelDiv.style.transform = `translate(-50%, -100%) translate(${x}px,${y}px)`;
            robotLabelDiv.style.display = tempV.z < 1 ? 'block' : 'none';
        }

        // Function to dispatch robot to target bin
        function dispatchRobotToBin(bin) {
            if (!bin) return;
            targetBinData = bin;
            const targetPos = robotTargetFor(bin);

            robotPath = computePath({ x: currentRobotPos.x, z: currentRobotPos.z }, targetPos, maxBay);
            robotWaypointIndex = 0;
            isRobotMoving = true;
            beaconGroup.visible = false;
            updateRobotPathLine();

            if (walkAction) walkAction.timeScale = 1;

            if (dotNetHelper) {
                dotNetHelper.invokeMethodAsync("OnRobotStateChanged", "moving", bin.id);
            }
        }

        // 11. Raycasting for hover & click
        const raycaster = new THREE.Raycaster();
        const mouse = new THREE.Vector2();

        let hoveredMesh = null;
        let selectedMesh = null;

        function updatePointer(e) {
            const rect = renderer.domElement.getBoundingClientRect();
            mouse.x = ((e.clientX - rect.left) / rect.width) * 2 - 1;
            mouse.y = -((e.clientY - rect.top) / rect.height) * 2 + 1;
        }

        function onMouseMove(e) {
            updatePointer(e);
            raycaster.setFromCamera(mouse, camera);
            const intersects = raycaster.intersectObjects(binGroup.children);

            if (intersects.length > 0) {
                const mesh = intersects[0].object;
                if (hoveredMesh !== mesh) {
                    if (hoveredMesh && hoveredMesh !== selectedMesh) {
                        resetMeshStyle(hoveredMesh);
                    }
                    hoveredMesh = mesh;
                    if (hoveredMesh !== selectedMesh) {
                        highlightMesh(hoveredMesh, 1.06, 0.9, 0.4);
                    }
                    renderer.domElement.style.cursor = "pointer";
                }
            } else {
                if (hoveredMesh && hoveredMesh !== selectedMesh) {
                    resetMeshStyle(hoveredMesh);
                }
                hoveredMesh = null;
                renderer.domElement.style.cursor = "auto";
            }
        }

        function onClick(e) {
            updatePointer(e);
            raycaster.setFromCamera(mouse, camera);
            const intersects = raycaster.intersectObjects(binGroup.children);

            if (intersects.length > 0) {
                const mesh = intersects[0].object;
                if (selectedMesh && selectedMesh !== mesh) {
                    resetMeshStyle(selectedMesh);
                }
                selectedMesh = mesh;
                highlightMesh(selectedMesh, 1.12, 0.98, 0.65);

                dispatchRobotToBin(mesh.userData);

                if (dotNetHelper && mesh.userData) {
                    dotNetHelper.invokeMethodAsync("OnBinSelected", mesh.userData);
                }
            }
        }

        function highlightMesh(mesh, scale, opacity, emissiveInt) {
            mesh.scale.set(scale, scale, scale);
            mesh.material.opacity = opacity;
            mesh.material.emissiveIntensity = emissiveInt;
        }

        function resetMeshStyle(mesh) {
            if (!mesh) return;
            const bin = mesh.userData;
            const isMatchFilter = isBinMatchingFilter(bin, filterStatus);
            mesh.scale.set(1, 1, 1);
            mesh.material.opacity = getBinOpacity(bin, isMatchFilter);
            mesh.material.emissiveIntensity = getBinEmissiveIntensity(bin, isMatchFilter);
        }

        renderer.domElement.addEventListener("mousemove", onMouseMove);
        renderer.domElement.addEventListener("click", onClick);

        // 12. Animation Loop with Clock & Robot Motion
        let animId = null;
        const clock = new THREE.Clock();

        function animate() {
            animId = requestAnimationFrame(animate);
            const delta = clock.getDelta();

            // Update Robot GLTF Animations
            if (robotMixer) {
                robotMixer.update(delta);
            }

            // Move Robot along waypoints
            if (isRobotMoving && robotPath.length > 0 && robotWaypointIndex < robotPath.length) {
                const waypoint = robotPath[robotWaypointIndex];
                const dx = waypoint.x - currentRobotPos.x;
                const dz = waypoint.z - currentRobotPos.z;
                const dist = Math.sqrt(dx * dx + dz * dz);

                if (dist > 0.04) {
                    const step = Math.min(ROBOT_SPEED * delta, dist);
                    currentRobotPos.x += (dx / dist) * step;
                    currentRobotPos.z += (dz / dist) * step;

                    // Rotate robot towards movement direction
                    const targetAngle = Math.atan2(dx, dz);
                    let da = targetAngle - robotGroup.rotation.y;
                    da = Math.atan2(Math.sin(da), Math.cos(da));
                    robotGroup.rotation.y += da * 0.15;
                } else {
                    robotWaypointIndex += 1;
                    if (robotWaypointIndex >= robotPath.length) {
                        // Robot arrived at target bin
                        isRobotMoving = false;
                        robotPath = [];
                        robotWaypointIndex = 0;
                        if (robotPathLine) robotPathLine.visible = false;
                        if (walkAction) walkAction.timeScale = 0;

                        if (targetBinData) {
                            const bPos = binWorldPos(targetBinData);
                            beaconGroup.position.set(bPos.x, bPos.y, bPos.z);
                            beaconGroup.visible = true;

                            if (dotNetHelper) {
                                dotNetHelper.invokeMethodAsync("OnRobotStateChanged", "arrived", targetBinData.id);
                            }
                        }
                    }
                }
                robotGroup.position.set(currentRobotPos.x, 0, currentRobotPos.z);
                updateRobotPathLine();
            }

            // Pulsating effect for beacon beam
            if (beaconGroup.visible) {
                beamMesh.material.opacity = 0.25 + Math.sin(clock.getElapsedTime() * 4) * 0.15;
            }

            // Update floating 3D search tooltip position above Robot head
            updateRobotLabelPosition();

            controls.update();
            renderer.render(scene, camera);
        }
        animate();

        // 13. Handle Resize
        function onResize() {
            const w = container.clientWidth || width;
            const h = container.clientHeight || height;
            camera.aspect = w / h;
            camera.updateProjectionMatrix();
            renderer.setSize(w, h);
        }
        window.addEventListener("resize", onResize);

        // Instance object
        instances[containerId] = {
            debug: () => window.__warehouseDebug,
            destroy: function () {
                isDestroyed = true;
                if (animId) cancelAnimationFrame(animId);
                window.removeEventListener("resize", onResize);
                renderer.domElement.removeEventListener("mousemove", onMouseMove);
                renderer.domElement.removeEventListener("click", onClick);
                if (robotMixer) robotMixer.stopAllAction();
                if (robotModel) {
                    robotModel.traverse((child) => {
                        if (child.geometry) child.geometry.dispose();
                        if (child.material) {
                            const materials = Array.isArray(child.material) ? child.material : [child.material];
                            materials.forEach((material) => material.dispose());
                        }
                    });
                }
                if (robotLabelDiv && robotLabelDiv.parentNode) {
                    robotLabelDiv.parentNode.removeChild(robotLabelDiv);
                }
                if (container) container.innerHTML = "";
                delete instances[containerId];
            },
            setFilter: function (status) {
                filterStatus = status;
                binMeshMap.forEach((mesh, id) => {
                    resetMeshStyle(mesh);
                });
            },
            selectBin: function (binId) {
                const mesh = binMeshMap.get(binId);
                if (mesh) {
                    if (selectedMesh) resetMeshStyle(selectedMesh);
                    selectedMesh = mesh;
                    highlightMesh(selectedMesh, 1.12, 0.98, 0.65);
                    dispatchRobotToBin(mesh.userData);
                }
            },
            dispatchRobot: function (binId) {
                const mesh = binMeshMap.get(binId);
                if (mesh) {
                    dispatchRobotToBin(mesh.userData);
                }
            }
        };
    }

    function setWarehouseFilter(containerId, status) {
        if (instances[containerId]) {
            instances[containerId].setFilter(status);
        }
    }

    function selectWarehouseBin(containerId, binId) {
        if (instances[containerId]) {
            instances[containerId].selectBin(binId);
        }
    }

    function dispatchWarehouseRobot(containerId, binId) {
        if (instances[containerId]) {
            instances[containerId].dispatchRobot(binId);
        }
    }

    window.Warehouse3D = {
        init: initWarehouse3D,
        setFilter: setWarehouseFilter,
        selectBin: selectWarehouseBin,
        dispatchRobot: dispatchWarehouseRobot
    };
})();
