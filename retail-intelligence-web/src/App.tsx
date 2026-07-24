import { BrowserRouter, Routes, Route } from "react-router-dom";

import Layout from "./components/layout/Layout";

import ImportacionesPage from "./pages/ImportacionesPage";
import PedidosPage from "./pages/PedidosPage";
import PedidoDetallePage from "./pages/PedidoDetallePage";
import BarcodePage from "./pages/BarcodePage";

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

                </Route>

            </Routes>
        </BrowserRouter>
    );
}

export default App;