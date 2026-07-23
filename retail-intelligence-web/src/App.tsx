import { BrowserRouter, Routes, Route } from "react-router-dom";

import ImportacionesPage from "./pages/ImportacionesPage";
import PedidosPage from "./pages/PedidosPage";
import PedidoDetallePage from "./pages/PedidoDetallePage";
import BarcodePage from "./pages/BarcodePage";


function App() {
    return (
        <BrowserRouter>
            <Routes>
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
            </Routes>
        </BrowserRouter>
    );
}

export default App;