/**
 * Warehouse3D.js - Three.js WebGL 3D Warehouse Visualization
 * Synchronized with Zone Colors & Lighter Translucent Grey for Empty Bins
 */

(function () {
    const instances = {};

    const BIN_W = 1.4, BIN_H = 0.95, BIN_D = 1.1;
    const BAY_GAP = 0.35, AISLE_GAP = 5.5, LEVEL_GAP = 0.3;
    const EMPTY_COLOR = 0x94A3B8;    // Light slate grey for empty bins (xám nhạt)
    const WARNING_COLOR = 0xEF4444;  // Bright red for expiring items

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

        // 2. Camera setup
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

        const camDistance = Math.max((maxAisle + 1) * AISLE_GAP, (maxBay + 1) * (BIN_D + BAY_GAP)) * 0.8 + 10;
        camera.position.set(centerX + camDistance * 0.75, centerY + camDistance * 0.55, centerZ + camDistance * 0.85);

        // 3. Renderer setup
        const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
        renderer.setSize(width, height);
        renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        renderer.shadowMap.enabled = true;
        container.innerHTML = "";
        container.appendChild(renderer.domElement);

        // 4. OrbitControls
        const controls = new THREE.OrbitControls(camera, renderer.domElement);
        controls.enableDamping = true;
        controls.dampingFactor = 0.05;
        controls.maxPolarAngle = Math.PI / 2.02;
        controls.minDistance = 3;
        controls.maxDistance = Math.max(camDistance * 3, 120);
        controls.target.set(centerX, centerY, centerZ);
        controls.update();

        // 5. Lighting
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.7);
        scene.add(ambientLight);

        const dirLight1 = new THREE.DirectionalLight(0xffffff, 0.85);
        dirLight1.position.set(centerX + 10, centerY + 15, centerZ + 10);
        scene.add(dirLight1);

        const dirLight2 = new THREE.DirectionalLight(0x6B93FF, 0.35);
        dirLight2.position.set(centerX - 10, centerY + 10, centerZ - 10);
        scene.add(dirLight2);

        // 6. Floor & Grid
        const floorW = (maxAisle + 1) * AISLE_GAP + 6;
        const floorD = (maxBay + 1) * (BIN_D + BAY_GAP) + 6;

        const floorGeo = new THREE.PlaneGeometry(floorW, floorD);
        const floorMat = new THREE.MeshStandardMaterial({ color: 0x0B0F17, roughness: 0.8 });
        const floorMesh = new THREE.Mesh(floorGeo, floorMat);
        floorMesh.rotation.x = -Math.PI / 2;
        floorMesh.position.set(centerX, -0.01, centerZ);
        scene.add(floorMesh);

        const gridHelper = new THREE.GridHelper(Math.max(floorW, floorD), 30, 0x2A3446, 0x1C2432);
        gridHelper.position.set(centerX, 0, centerZ);
        scene.add(gridHelper);

        // 7. Rack metal posts
        const postMat = new THREE.MeshStandardMaterial({ color: 0x4A5568, metalness: 0.6, roughness: 0.4 });
        const postGeo = new THREE.BoxGeometry(0.08, (maxLevel + 1) * (BIN_H + LEVEL_GAP) + 0.3, 0.08);

        for (let a = 0; a <= maxAisle; a++) {
            for (let corner = 0; corner < 2; corner++) {
                const z = corner === 0 ? -0.4 : maxBay * (BIN_D + BAY_GAP) + 0.4;
                const post = new THREE.Mesh(postGeo, postMat);
                post.position.set(a * AISLE_GAP, ((maxLevel + 1) * (BIN_H + LEVEL_GAP)) / 2, z);
                scene.add(post);
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

        // 9. Raycasting for hover & click
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

        // 10. Animation Loop
        let animId = null;
        function animate() {
            animId = requestAnimationFrame(animate);
            controls.update();
            renderer.render(scene, camera);
        }
        animate();

        // 11. Handle Resize
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
            destroy: function () {
                if (animId) cancelAnimationFrame(animId);
                window.removeEventListener("resize", onResize);
                renderer.domElement.removeEventListener("mousemove", onMouseMove);
                renderer.domElement.removeEventListener("click", onClick);
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

    window.Warehouse3D = {
        init: initWarehouse3D,
        setFilter: setWarehouseFilter,
        selectBin: selectWarehouseBin
    };
})();
