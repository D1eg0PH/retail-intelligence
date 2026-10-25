import { BrowserRouter, Routes, Route } from "react-router-dom";

import Layout from "./components/layout/Layout";

import ImportacionesPage from "./pages/ImportacionesPage";
import PedidosPage from "./pages/PedidosPage";
import PedidoDetallePage from "./pages/PedidoDetallePage";
import BarcodePage from "./pages/BarcodePage";
import ConfiguracionPage from "./pages/ConfiguracionPage";
import DepuracionProductosPage from "./pages/DepuracionProductosPage";
import EleventaDashboardPage from "./pages/EleventaDashboardPage";
import EleventaProductCleanupPage from "./pages/EleventaProductCleanupPage";
import EleventaOrderSuggestionsPage from "./pages/EleventaOrderSuggestionsPage";

function App() {
    return (
        <BrowserRouter>
            <Routes>

                <Route element={<Layout />}>

                    <Route
                        path="/"
                        element={<ImportacionesPage />}
                    />

                    <Route
                        path="/pedidos"
                        element={<PedidosPage />}
                    />

                    <Route
                        path="/pedidos/:id"
                        element={<PedidoDetallePage />}
                    />

                    <Route
                        path="/barcodes"
                        element={<BarcodePage />}
                    />
                    <Route
                        path="/configuracion"
                        element={<ConfiguracionPage />}
                    />

                    <Route
                        path="/dashboard"
                        element={<DepuracionProductosPage />}
                    />

                    <Route
                        path="/dashboard/eleventa"
                        element={<EleventaDashboardPage />}
                    />

                    <Route
                        path="/dashboard/eleventa/depurador"
                        element={<EleventaProductCleanupPage />}
                    />

                    <Route
                        path="/dashboard/eleventa/pedidos"
                        element={<EleventaOrderSuggestionsPage />}
                    />

                </Route>

            </Routes>
        </BrowserRouter>
    );
}

export default App;
